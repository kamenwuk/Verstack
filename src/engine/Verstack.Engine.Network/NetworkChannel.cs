using Verstack.Engine.Network.Packet;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Verstack.Engine.Network;

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

    public volatile int CompressionThreshold = -1;
    
    public readonly ConcurrentQueue<RawPacket> IncomingPackets = new();
    internal readonly ConcurrentQueue<OutboundSegment> OutboundQueue = new();

    private readonly SemaphoreSlim _outboundSignal = new(0, int.MaxValue);
    public readonly string RemoteAddress;

    private int _isDisconnected = 0;
    public bool IsDisconnected => _isDisconnected == 1;

    public NetworkChannel(Socket socket)
    {
        Socket = socket;
        try { RemoteAddress = socket.RemoteEndPoint?.ToString() ?? "Unknown"; }
        catch { RemoteAddress = "Unknown"; }

        var stream = new NetworkStream(socket, ownsSocket: true);
        Reader = PipeReader.Create(stream);
        Writer = PipeWriter.Create(stream);
    }

    public void EnqueueOutbound(ReadOnlySpan<byte> data)
    {
        OutboundQueue.Enqueue(OutboundSegment.Rent(data));
        _outboundSignal.Release();
    }

    public void EnqueueOutbound(byte[] rentedBuffer, int length)
    {
        OutboundQueue.Enqueue(OutboundSegment.FromRentedArray(rentedBuffer, length));
        _outboundSignal.Release();
    }

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
        catch
        {
            // ignored
        }

        try
        {
            if (Socket.Connected)
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            Socket.Close();
        }

        // КРИТИЧЕСКИ ВАЖНО: Будим Send-воркер, иначе он уснет навсегда в WaitOutboundAsync
        _outboundSignal.Release();
    }
}
