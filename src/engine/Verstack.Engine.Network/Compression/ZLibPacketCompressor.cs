using System.IO.Compression;
using System.Buffers;

namespace Verstack.Engine.Network.Compression;

/// <summary>
/// Компрессор пакетов на базе <see cref="ZLibStream"/> (формат RFC 1950 / zlib).
/// </summary>
public sealed class ZLibPacketCompressor : IPacketCompressor
{
    /// <inheritdoc/>
    public int GetMaxCompressedSize(int sourceLength)
    {
        // compressBound из zlib-документации: верхняя оценка без переполнения.
        return sourceLength + (sourceLength >> 12) + (sourceLength >> 14) + 64;
    }

    /// <inheritdoc/>
    public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        // ZLibStream не умеет писать напрямую в Span — арендуем массив, пишем, копируем.
        byte[] rented = ArrayPool<byte>.Shared.Rent(destination.Length);
        try
        {
            int compressedLength;
            using (var ms = new MemoryStream(rented, 0, rented.Length))
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(source);
                zlib.Flush();
                compressedLength = (int)ms.Position;
            }

            rented.AsSpan(0, compressedLength).CopyTo(destination);
            return compressedLength;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}