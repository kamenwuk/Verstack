using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Writes a Verstack.Minecraft frame — <c>[VarInt(length)][payload]</c> — into an
/// <see cref="IBufferWriter{T}"/>. The mirror of <see cref="PacketFrameScanner"/>.
/// </summary>
/// <remarks>
/// Pure framing logic, no transport coupling: feed it an
/// <see cref="IBufferWriter{T}"/> (a <c>PipeWriter</c> in production, an
/// <c>ArrayBufferWriter&lt;byte&gt;</c> in tests) and a payload — out comes a
/// complete frame. <see cref="PacketFrameScanner"/> reads frames out of a
/// <see cref="ReadOnlySequence{T}"/>; <see cref="PacketFraming"/> writes frames
/// into a buffer.
/// </remarks>
public static class PacketFraming
{
    /// <summary>Default Verstack.Minecraft frame size limit, in bytes (~2 MB).</summary>
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    /// <summary>
    /// Wraps <paramref name="payload"/> in a VarInt length prefix and writes the
    /// complete frame to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Buffer to write the frame into (e.g. a <c>PipeWriter</c>).</param>
    /// <param name="payload">Raw packet body, written as-is after the length prefix.</param>
    /// <remarks>
    /// Atomic: requests a single span sized to the whole frame and commits once,
    /// so the length prefix and payload land contiguously — the reader never has
    /// to stitch them back together across segments.
    /// </remarks>
    public static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
    {
#if DEBUG
        // Превышение лимита — баг в нашем сериализаторе, не сетевая атака:
        // writer'а вызываем мы сами, payload формируем сами. Поэтому проверка
        // только в дебаге; в релизе не тратим такты. Асимметрия со scanner'ом
        // осознанная: тот валидирует данные из ненадёжного потока в рантайме.
        if (payload.Length > DEFAULT_MAX_PACKET_SIZE)
            throw new ArgumentException(
                $"[{nameof(PacketFraming)}] Payload exceeds max packet size ({payload.Length} > {DEFAULT_MAX_PACKET_SIZE}).",
                nameof(payload));
#endif

        int lengthBytes = VarInt.GetByteCount(payload.Length);
        Span<byte> span = output.GetSpan(lengthBytes + payload.Length);

        int written = VarInt.Encode(payload.Length, span);
        payload.CopyTo(span[written..]);

        output.Advance(written + payload.Length);
    }
}