using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Writes VarInt-length-prefixed frames to an <see cref="IBufferWriter{T}"/>.
/// </summary>
public static class PacketFrameWriter
{
    /// <summary>Default maximum packet size (2 MB).</summary>
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    /// <summary>
    /// Wraps <paramref name="payload"/> with a VarInt length prefix and writes the frame to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The buffer to write to (e.g. a <c>PipeWriter</c>).</param>
    /// <param name="payload">The packet body.</param>
    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > DEFAULT_MAX_PACKET_SIZE)
            throw new ArgumentException(
                $"Payload exceeds maximum packet size ({payload.Length} > {DEFAULT_MAX_PACKET_SIZE}).",
                nameof(payload));

        int lengthBytes = VarInt.GetByteCount(payload.Length);
        Span<byte> span = output.GetSpan(lengthBytes + payload.Length);

        int written = VarInt.Encode(payload.Length, span);
        payload.CopyTo(span[written..]);

        output.Advance(written + payload.Length);
    }
}