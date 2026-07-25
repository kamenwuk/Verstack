using System.IO.Compression;
using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Default decompressor using <see cref="ZLibStream"/>.
/// </summary>
public sealed class ZLibPacketDecompressor : IPacketDecompressor
{
    /// <inheritdoc/>
    public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
    {
        byte[] inputBuffer = ArrayPool<byte>.Shared.Rent((int)source.Length);
        try
        {
            source.CopyTo(inputBuffer);
            using var ms = new MemoryStream(inputBuffer, 0, (int)source.Length);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
            
            byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                int totalRead = 0;
                while (totalRead < destination.Length)
                {
                    int read = zlib.Read(outputBuffer, totalRead, destination.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                outputBuffer.AsSpan(0, totalRead).CopyTo(destination);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outputBuffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
        }
    }
}