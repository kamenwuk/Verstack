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

    /// <param name="handler">Reacts to each frame's payload; may write
    /// responses to the connection's output.</param>
    public SessionLifetime(IPacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
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

                // Scanner одноразовый, на один ReadAsync: после AdvanceTo буфер невалиден.
                var frameReader = new PacketFrameReader(result.Buffer);

                // Disconnect, запрошенный handler'ом внутри scanner-цикла:
                // выходим из цикла, но не из read-цикла (выход — ниже, после flush).
                bool drop = false;
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

                // Scanner — ref struct, не может жить через await. Поэтому вычитываем
                // всё нужное в обычные value-локалы ДО await, после чего scanner «умирает».
                SequencePosition consumed = frameReader.ConsumedPosition;
                VarInt.ReadStatus status = frameReader.Status;

                // consumed=позиция scanner'а, examined=конец буфера.
                // При Partial consumed = начало недочитанного кадра →
                // Pipe оставит хвост и подбросит ещё данных.
                reader.AdvanceTo(consumed, result.Buffer.End);

                // Handler пишет в буфер sync; flush — наша ответственность,
                // чтобы контролировать точку flush'а (и будущий batching).
                await writer.FlushAsync(token).ConfigureAwait(false);

                // Drop по вердикту handler'а или Malformed-кадру: причина уже
                // залогирована (handler'ом или тут, если Malformed-кадр), рвём.
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
            // Финализация Pipe обязательна — иначе утечка. CompleteAsync в finally,
            // чтобы сработало при любом выходе (отмена, ошибка, Malformed).
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