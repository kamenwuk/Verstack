using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Abstracts packet decompression logic for the framing layer.
/// </summary>
public interface IPacketDecompressor
{
    /// <summary>
    /// Decompresses <paramref name="source"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">Compressed data.</param>
    /// <param name="destination">Destination span. Must be exactly <c>dataLength</c> in size.</param>
    void Decompress(ReadOnlySequence<byte> source, Span<byte> destination);
}