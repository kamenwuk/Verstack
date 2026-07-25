namespace Verstack.Protocol;

/// <summary>
/// Abstracts packet compression logic for the framing layer.
/// </summary>
public interface IPacketCompressor
{
    /// <summary>
    /// Returns the maximum possible size of the compressed data for a given source length.
    /// </summary>
    /// <param name="sourceLength">Length of the uncompressed payload.</param>
    /// <returns>Maximum required buffer size for compression.</returns>
    int GetMaxCompressedSize(int sourceLength);

    /// <summary>
    /// Compresses <paramref name="source"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">Uncompressed payload.</param>
    /// <param name="destination">Destination span. Must be at least <see cref="GetMaxCompressedSize"/> bytes long.</param>
    /// <returns>The number of compressed bytes written.</returns>
    int Compress(ReadOnlySpan<byte> source, Span<byte> destination);
}