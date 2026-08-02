using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using BenchmarkDotNet.Attributes;
using Verstack.Network.Packet;
using System.IO.Compression;
using System.Buffers;

namespace Verstack.Network.Benchmarks;

[MemoryDiagnoser]
public class PacketFrameBenchmarks
{
    private byte[] _frameWriterBuffer = null!;
    private byte[] _payload = null!;
    private ReadOnlySequence<byte> _uncompressedFrame;
    private ReadOnlySequence<byte> _compressedFrame;

    private BenchmarkCompressor _compressor = null!;
    private BenchmarkDecompressor _decompressor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compressor = new BenchmarkCompressor();
        _decompressor = new BenchmarkDecompressor();
        _frameWriterBuffer = ArrayPool<byte>.Shared.Rent(4096);

        _payload = new byte[128];
        new Random(42).NextBytes(_payload);
        var payloadWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(256));
        payloadWriter.WriteVarInt(0x05).WriteSpan(_payload);
        _payload = payloadWriter.WrittenSpan.ToArray();

        var frameWriterUncomp = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(512));
        PacketFrame.Write(ref frameWriterUncomp, _payload, null, -1);
        _uncompressedFrame = new ReadOnlySequence<byte>(frameWriterUncomp.WrittenSpan.ToArray());

        var frameWriterComp = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(512));
        PacketFrame.Write(ref frameWriterComp, _payload, _compressor, 10);
        _compressedFrame = new ReadOnlySequence<byte>(frameWriterComp.WrittenSpan.ToArray());
    }

    [Benchmark(Baseline = true)]
    public void Frame_Write_Uncompressed()
    {
        var writer = new PacketStreamWriter(_frameWriterBuffer);
        PacketFrame.Write(ref writer, _payload, null, -1);
    }

    [Benchmark]
    public void Frame_Write_Compressed()
    {
        var writer = new PacketStreamWriter(_frameWriterBuffer);
        PacketFrame.Write(ref writer, _payload, _compressor, 10);
    }

    [Benchmark]
    public void Frame_Read_Uncompressed()
    {
        PacketFrame.TryRead(_uncompressedFrame, -1, _decompressor, out _, out _, out _);
    }

    [Benchmark]
    public void Frame_Read_Compressed()
    {
        PacketFrame.TryRead(_compressedFrame, 10, _decompressor, out _, out _, out _);
    }
    
    private class BenchmarkCompressor : IPacketCompressor
    {
        public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            using var ms = new MemoryStream();
            using var zs = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true);
            zs.Write(source);
            zs.Flush();
            byte[] compressed = ms.ToArray();
            compressed.CopyTo(destination);
            return compressed.Length;
        }

        public int GetMaxCompressedSize(int size) => size + 64;
    }

    private class BenchmarkDecompressor : IPacketDecompressor
    {
        public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
        {
            byte[] src = source.ToArray();
            using var ms = new MemoryStream(src);
            using var zs = new ZLibStream(ms, CompressionMode.Decompress);
            int totalRead = 0;
            while (totalRead < destination.Length)
            {
                int read = zs.Read(destination.Slice(totalRead));
                if (read == 0) break;
                totalRead += read;
            }
        }
    }
}