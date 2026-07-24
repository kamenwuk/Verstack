using System.IO.Pipelines;
using Verstack.Protocol;
using System.Buffers;

namespace Verstack.Network;

/// <summary>
/// Manages the entire lifetime of a single connection: read loop,
/// framing via <see cref="PacketFrameScanner"/>, and frame dispatch.
/// </summary>
/// <remarks>
/// Receives <see cref="IDuplexPipe"/> (not <c>SocketConnection</c>) so it stays
/// decoupled from the transport implementation — depends only on the
/// PipeReader/PipeWriter contract.
/// </remarks>
public sealed class SessionLifetime
{
    /// <summary>
    /// Drives the connection's lifetime from start to disconnect:
    /// reads, frames, dispatches, finalizes the pipe.
    /// </summary>
    public async Task RunAsync(IDuplexPipe connection, CancellationToken token)
    {
        PipeReader reader = connection.Input;
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
                var scanner = new PacketFrameScanner(result.Buffer);

                while (scanner.MoveNext())
                {
                    LogFrame(scanner.Current);
                }

                // consumed=позиция scanner's, examined=конец буфера.
                // При Partial scanner.ConsumedPosition = начало недочитанного кадра →
                // Pipe оставит хвост и подбросит ещё данных.
                reader.AdvanceTo(scanner.ConsumedPosition, result.Buffer.End);

                if (scanner.Status == VarInt.ReadStatus.Malformed)
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
}