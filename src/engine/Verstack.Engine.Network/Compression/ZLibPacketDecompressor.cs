using System.IO.Compression;
using System.Buffers;

namespace Verstack.Engine.Network.Compression;

/// <summary>
/// Декомпрессор пакетов на базе <see cref="ZLibStream"/> (формат RFC 1950 / zlib).
/// </summary>
public sealed class ZLibPacketDecompressor : IPacketDecompressor
{
    /// <inheritdoc/>
    public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
    {
        byte[] input = ArrayPool<byte>.Shared.Rent((int)source.Length);
        try
        {
            source.CopyTo(input);
            using var ms = new MemoryStream(input, 0, (int)source.Length);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);

            byte[] output = ArrayPool<byte>.Shared.Rent(destination.Length);
            try
            {
                int totalRead = 0;
                while (totalRead < destination.Length)
                {
                    int read = zlib.Read(output, totalRead, destination.Length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
                output.AsSpan(0, totalRead).CopyTo(destination);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(output);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(input);
        }
    }
}