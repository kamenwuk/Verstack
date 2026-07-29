using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

[ShortRunJob]
[MemoryDiagnoser]
public class PacketFrameWithZLibBenchmarks
{
    private byte[] _smallPayload;
    private byte[] _mediumPayload;
    private byte[] _frameScratch;
    private ZLibPacketCompressor _zlibCompressor;
    private ZLibPacketDecompressor _zlibDecompressor;
    private const int MaxFrameSize = 65536;

    [GlobalSetup]
    public void Setup()
    {
        _smallPayload = new byte[100];
        _mediumPayload = new byte[1024];
        new Random(42).NextBytes(_smallPayload);
        new Random(42).NextBytes(_mediumPayload);

        _frameScratch = new byte[MaxFrameSize];
        _zlibCompressor = new ZLibPacketCompressor();
        _zlibDecompressor = new ZLibPacketDecompressor();
    }

    [Benchmark]
    public int Write_Uncompressed_BelowThreshold()
    {
        var writer = new SpanWriter(_frameScratch);
        PacketFrame.Write(ref writer, _smallPayload, _zlibCompressor, 256);
        return writer.Written;
    }

    [Benchmark]
    public int Write_Compressed_AboveThreshold()
    {
        var writer = new SpanWriter(_frameScratch);
        PacketFrame.Write(ref writer, _mediumPayload, _zlibCompressor, 256);
        return writer.Written;
    }

    private ReadOnlySequence<byte> PrepareFrame(ReadOnlySpan<byte> payload, int threshold)
    {
        var buf = new ArrayBufferWriter<byte>();
        var sw = new SpanWriter(buf.GetSpan(2048));
        PacketFrame.Write(ref sw, payload, _zlibCompressor, threshold);
        buf.Advance(sw.Written);
        return new ReadOnlySequence<byte>(buf.WrittenMemory);
    }

    [Benchmark]
    public PacketFrameResult TryRead_Uncompressed_BelowThreshold()
    {
        var seq = PrepareFrame(_smallPayload, 256);
        return PacketFrame.TryRead(seq, 256, _zlibDecompressor, out _, out _, out _);
    }

    [Benchmark]
    public PacketFrameResult TryRead_Compressed_AboveThreshold()
    {
        var seq = PrepareFrame(_mediumPayload, 256);
        return PacketFrame.TryRead(seq, 256, _zlibDecompressor, out _, out _, out _);
    }
}