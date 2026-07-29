using Verstack.Network.Compression;
using System.Buffers;

namespace Verstack.Network.Tests;

public sealed class ZLibCompressionTests
{
    [Fact]
    public void CompressDecompress_Roundtrip_SmallData()
    {
        var compressor = new ZLibPacketCompressor();
        var decompressor = new ZLibPacketDecompressor();

        byte[] original = new byte[100];
        new Random(42).NextBytes(original);

        byte[] compressed = new byte[compressor.GetMaxCompressedSize(original.Length)];
        int compressedLen = compressor.Compress(original, compressed);

        byte[] decompressed = new byte[original.Length];
        var seq = new ReadOnlySequence<byte>(compressed.AsMemory(0, compressedLen));
        decompressor.Decompress(seq, decompressed);

        Assert.Equal(original, decompressed);
    }
}