using System.IO.Compression;
using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Default compressor using <see cref="ZLibStream"/> (RFC 1950 format).
/// </summary>
public sealed class ZLibPacketCompressor : IPacketCompressor
{
    /// <inheritdoc/>
    public int GetMaxCompressedSize(int sourceLength)
    {
        // Формула compressBound из zlib-документации
        return sourceLength + (sourceLength >> 12) + (sourceLength >> 14) + 64;
    }

    /// <inheritdoc/>
    public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(destination.Length);
        try
        {
            int compressedLength;
            using (var ms = new MemoryStream(rentedBuffer, 0, rentedBuffer.Length))
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(source);
                zlib.Flush();
                compressedLength = (int)ms.Position;
            }

            rentedBuffer.AsSpan(0, compressedLength).CopyTo(destination);
            return compressedLength;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}