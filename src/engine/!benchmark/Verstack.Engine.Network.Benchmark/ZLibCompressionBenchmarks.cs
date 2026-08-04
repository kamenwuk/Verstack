using Verstack.Engine.Network.Compression;
using BenchmarkDotNet.Attributes;
using System.Buffers;

namespace Verstack.Engine.Network.Benchmark;

[ShortRunJob]
[MemoryDiagnoser]
public class ZLibCompressionBenchmarks
{
    private ZLibPacketCompressor _compressor = null!;
    private ZLibPacketDecompressor _decompressor = null!;
    private byte[] _smallPayload = null!;
    private byte[] _mediumPayload = null!;
    private byte[] _largePayload = null!;
    private byte[] _compressedSmall = null!;
    private byte[] _compressedMedium = null!;
    private byte[] _compressedLarge = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compressor = new ZLibPacketCompressor();
        _decompressor = new ZLibPacketDecompressor();

        _smallPayload = new byte[100];
        _mediumPayload = new byte[1024];
        _largePayload = new byte[10240];
        new Random(42).NextBytes(_smallPayload);
        new Random(42).NextBytes(_mediumPayload);
        new Random(42).NextBytes(_largePayload);

        _compressedSmall = CompressToArray(_smallPayload);
        _compressedMedium = CompressToArray(_mediumPayload);
        _compressedLarge = CompressToArray(_largePayload);
    }

    private byte[] CompressToArray(byte[] source)
    {
        var tmp = new byte[_compressor.GetMaxCompressedSize(source.Length)];
        int written = _compressor.Compress(source, tmp);
        return tmp.AsSpan(0, written).ToArray();
    }

    [Benchmark]
    public int Compress_Small() => _compressor.Compress(_smallPayload, stackalloc byte[_compressor.GetMaxCompressedSize(_smallPayload.Length)]);

    [Benchmark]
    public int Compress_Medium() => _compressor.Compress(_mediumPayload, stackalloc byte[_compressor.GetMaxCompressedSize(_mediumPayload.Length)]);

    [Benchmark]
    public int Compress_Large()
    {
        byte[] dest = new byte[_compressor.GetMaxCompressedSize(_largePayload.Length)];
        return _compressor.Compress(_largePayload, dest);
    }

    [Benchmark]
    public void Decompress_Small()
    {
        var seq = new ReadOnlySequence<byte>(_compressedSmall);
        Span<byte> dest = stackalloc byte[_smallPayload.Length];
        _decompressor.Decompress(seq, dest);
    }

    [Benchmark]
    public void Decompress_Medium()
    {
        var seq = new ReadOnlySequence<byte>(_compressedMedium);
        Span<byte> dest = stackalloc byte[_mediumPayload.Length];
        _decompressor.Decompress(seq, dest);
    }

    [Benchmark]
    public void Decompress_Large()
    {
        var seq = new ReadOnlySequence<byte>(_compressedLarge);
        Span<byte> dest = new byte[_largePayload.Length];
        _decompressor.Decompress(seq, dest);
    }
}