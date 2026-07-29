using System.Collections.Concurrent;
using Verstack.Network.Packet;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Verstack.Network;

/// <summary>
/// Обёртка над одним TCP-соединением. Мост между пассивным насосом байт (Network) и слоями.
///
/// Две очереди развязывают потоки и ECS:
/// <list type="bullet">
///   <item><see cref="IncomingPackets"/> — read-поток → ECS-тик (Leopotam не потокобезопасен).</item>
///   <item><see cref="OutboundQueue"/> + <see cref="_outboundSignal"/> — ECS-тик → send-воркер.
///     Владелец <see cref="Writer"/> (PipeWriter) — ТОЛЬКО send-воркер:
///     контракт <c>System.IO.Pipelines</c> требует single writer, ECS не трогает Writer напрямую.</item>
/// </list>
/// </summary>
public class NetworkChannel
{
    public readonly Socket Socket;
    public readonly PipeReader Reader;
    public readonly PipeWriter Writer;

    /// <summary>
    /// Порог сжатия пакетов (Set Compression) для этого канала. -1 — compression выключена
    /// (несжатый framing), ≥ 0 — compressed framing по протоколу Minecraft.
    ///
    /// Записывается ECS-потоком (<c>PacketDispatchSystem</c> при отправке Set Compression),
    /// читается read-потоком (<c>TcpNetworkService.TryReadPacket</c>) — поэтому <see cref="volatile"/>:
    /// атомарная видимость значения между потоками без захода в ECS-мир из read-цикла.
    /// Симметрично <see cref="_isDisconnected"/> по модели cross-thread флага на канале.
    /// </summary>
    public volatile int CompressionThreshold = -1;
    
    // Потокобезопасная очередь входящих пакетов: read-поток → ECS-система.
    public readonly ConcurrentQueue<RawPacket> IncomingPackets = new();

    // Потокобезопасная очередь исходящих чанков: ECS-система → send-воркер.
    internal readonly ConcurrentQueue<OutboundChunk> OutboundQueue = new();

    // Сигнал send-воркеру: «в OutboundQueue есть данные, проснись и флашь».
    // SemaphoreSlim(1) — воркер WaitAsync ждёт Release от ECS.
    private readonly SemaphoreSlim _outboundSignal = new(0, int.MaxValue);

    public readonly string RemoteAddress;

    private int _isDisconnected = 0;

    public NetworkChannel(Socket socket)
    {
        Socket = socket;
        try { RemoteAddress = socket.RemoteEndPoint?.ToString() ?? "Unknown"; }
        catch { RemoteAddress = "Unknown"; }

        var stream = new NetworkStream(socket, ownsSocket: true);
        Reader = PipeReader.Create(stream);
        Writer = PipeWriter.Create(stream);
    }

    /// <summary>
    /// Ставит порцию байтов в очередь на отправку и будит send-воркер.
    /// Вызывается ECS-системой в главном тике: копирует <paramref name="data"/> в арендованный буфер
    /// и сигнализирует. Сам <see cref="Writer"/> не трогает — это обязанность send-воркера
    /// (single-writer контракт Pipes).
    /// </summary>
    public void EnqueueOutbound(ReadOnlySpan<byte> data)
    {
        OutboundQueue.Enqueue(OutboundChunk.Rent(data));
        _outboundSignal.Release();
    }

    /// <summary>
    /// Ждёт появления данных в <see cref="OutboundQueue"/>. Вызывается send-воркером.
    /// </summary>
    internal Task WaitOutboundAsync(CancellationToken ct) => _outboundSignal.WaitAsync(ct);

    public void Disconnect()
    {
        if (Interlocked.CompareExchange(ref _isDisconnected, 1, 0) != 0)
            return;

        try
        {
            Reader.Complete();
            Writer.Complete();
        }
        catch { /* Игнорируем ошибки завершения пайпов */ }

        try
        {
            // Отключаем сокет, только если он ещё не disposed
            if (Socket.Connected)
            {
                Socket.Disconnect(false);
            }
        }
        catch { /* Игнорируем ошибки сокета при отключении */ }
        finally
        {
            Socket.Dispose();
        }
    }
}
