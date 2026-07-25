using System.IO.Pipelines;
using Verstack.Protocol;
using System.Buffers;

namespace Verstack.Network;

/// <summary>
/// Manages the entire lifetime of a single connection: read loop,
/// framing via <see cref="PacketFrameReader"/>, frame dispatch to an
/// <see cref="IPacketHandler"/>, and finalization.
/// </summary>
/// <remarks>
/// Receives <see cref="IDuplexPipe"/> (not <c>SocketConnection</c>) so it stays
/// decoupled from the transport implementation — depends only on the
/// PipeReader/PipeWriter contract. Packet handling is delegated to an
/// <see cref="IPacketHandler"/>: SessionLifetime frames and flushes, the handler
/// forms responses.
/// </remarks>
public sealed class SessionLifetime
{
    private readonly IPacketHandler _handler;
    private readonly IPacketDecompressor? _decompressor;

    /// <param name="handler">Reacts to each frame's payload; may write
    /// responses to the connection's output.</param>
    /// <param name="decompressor">Decompressor instance. If null, compression is disabled.</param>
    public SessionLifetime(IPacketHandler handler, IPacketDecompressor? decompressor = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
        _decompressor = decompressor;
    }

    /// <summary>
    /// Drives the connection's lifetime from start to disconnect:
    /// reads, frames, dispatches, finalizes the pipe.
    /// </summary>
    public async Task RunAsync(IDuplexPipe connection, CancellationToken token)
    {
        PipeReader reader = connection.Input;
        PipeWriter writer = connection.Output;
        try
        {
            while (!token.IsCancellationRequested)
            {
                ReadResult result;
                try
                {
                    result = await reader.ReadAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break; // штатная остановка по токену
                }

                if (result.IsCanceled)
                    break;

                bool drop = false;
                SequencePosition consumed;
                VarInt.ReadStatus status;

                // Классический using-блок ограничивает область видимости ref struct.
                // Dispose вызовется на закрывающей скобке, ДО await FlushAsync ниже.
                using (var frameReader = new PacketFrameReader(result.Buffer, decompressor: _decompressor))
                {
                    while (frameReader.MoveNext())
                    {
#if DEBUG
                        LogFrame(frameReader.Current);
#endif
                        if (_handler.OnPacket(frameReader.Current, writer) == PacketVerdict.Disconnect)
                        {
                            drop = true;
                            break;
                        }
                    }
                    // Сохраняем значения до выхода из using-блока
                    consumed = frameReader.ConsumedPosition;
                    status = frameReader.Status;
                } // Здесь фрейм ридер "умирает" и возвращает буферы в ArrayPool

                reader.AdvanceTo(consumed, result.Buffer.End);

                // Handler пишет в буфер sync; flush — наша ответственность,
                // чтобы контролировать точку flush'а (и будущий batching).
                await writer.FlushAsync(token).ConfigureAwait(false);

                if (drop || status == VarInt.ReadStatus.Malformed)
                {
                    Console.WriteLine($"[{nameof(SessionLifetime)}] Malformed frame — dropping connection.");
                    break;
                }

                if (result.IsCompleted)
                    break; // peer закрыл отправляющую сторону
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

#if DEBUG
    /// <summary>
    /// Отладочный дамп кадра: длина + hex первых байт. Заменится парсером пакетов.
    /// </summary>
    private static void LogFrame(ReadOnlySequence<byte> payload)
    {
        int dumpLen = (int)Math.Min(payload.Length, 16);
        Span<byte> head = stackalloc byte[dumpLen];
        payload.Slice(0, dumpLen).CopyTo(head);

        Console.WriteLine($"[{nameof(SessionLifetime)}] Frame len={payload.Length}, head={ToHex(head)}");
    }

    /// <summary>
    /// Hex-дамп без LINQ/StringBuilder — GC-friendly для лога.
    /// </summary>
    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        const string HEX = "0123456789ABCDEF";
        Span<char> chars = stackalloc char[bytes.Length * 3];
        for (int idx = 0; idx < bytes.Length; idx++)
        {
            byte data = bytes[idx];
            chars[idx * 3] = HEX[data >> 4];
            chars[idx * 3 + 1] = HEX[data & 0x0F];
            chars[idx * 3 + 2] = ' ';
        }
        return new string(chars.TrimEnd());
    }
#endif
}