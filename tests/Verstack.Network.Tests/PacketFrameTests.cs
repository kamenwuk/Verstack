using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using System.Buffers;

namespace Verstack.Network.Tests;

public sealed class PacketFrameTests
{
    private readonly IPacketCompressor _compressor = new IdentityCompressor();
    private readonly IPacketDecompressor _decompressor = new IdentityDecompressor();

    private static byte[] CreateTestPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        VarInt.Write(writer, 42); // packetId
        writer.Write(new byte[200]); // data
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void WriteRead_UncompressedFrame_Roundtrip()
    {
        var payload = CreateTestPayload();
        byte[] frame = new byte[1024];
        var writer = new SpanWriter(frame);
        PacketFrame.Write(ref writer, payload, _compressor, -1);
        var seq = new ReadOnlySequence<byte>(frame.AsMemory(0, writer.Written));

        var result = PacketFrame.TryRead(seq, -1, _decompressor, out int id, out byte[] data, out _);
        Assert.Equal(PacketFrameResult.Complete, result);
        Assert.Equal(42, id);
        Assert.Equal(200, data.Length);
    }

    [Fact]
    public void WriteRead_CompressedBelowThreshold_Roundtrip()
    {
        var payload = CreateTestPayload();
        byte[] frame = new byte[1024];
        var writer = new SpanWriter(frame);
        PacketFrame.Write(ref writer, payload, _compressor, 1000); // threshold > size => DataLength=0
        var seq = new ReadOnlySequence<byte>(frame.AsMemory(0, writer.Written));

        var result = PacketFrame.TryRead(seq, 1000, _decompressor, out int id, out byte[] data, out _);
        Assert.Equal(PacketFrameResult.Complete, result);
        Assert.Equal(42, id);
        Assert.Equal(200, data.Length);
    }

    [Fact]
    public void WriteRead_CompressedAboveThreshold_Roundtrip()
    {
        var payload = CreateTestPayload();
        byte[] frame = new byte[1024];
        var writer = new SpanWriter(frame);
        PacketFrame.Write(ref writer, payload, _compressor, 50); // threshold < size => compress
        var seq = new ReadOnlySequence<byte>(frame.AsMemory(0, writer.Written));

        var result = PacketFrame.TryRead(seq, 50, _decompressor, out int id, out byte[] data, out _);
        Assert.Equal(PacketFrameResult.Complete, result);
        Assert.Equal(42, id);
        Assert.Equal(200, data.Length);
    }

    [Fact]
    public void TryRead_PartialBuffer_ReturnsPartial()
    {
        var payload = CreateTestPayload();
        byte[] frame = new byte[1024];
        var writer = new SpanWriter(frame);
        PacketFrame.Write(ref writer, payload, _compressor, -1);
        // обрезаем до 1 байта
        var seq = new ReadOnlySequence<byte>(frame.AsMemory(0, 1));

        var result = PacketFrame.TryRead(seq, -1, _decompressor, out _, out _, out _);
        Assert.Equal(PacketFrameResult.Partial, result);
    }

    [Fact]
    public void TryRead_MalformedLength_ReturnsMalformed()
    {
        byte[] frame = new byte[] { 0x00 }; // VarInt = 0, недопустимая длина
        var seq = new ReadOnlySequence<byte>(frame);
        var result = PacketFrame.TryRead(seq, -1, _decompressor, out _, out _, out _);
        Assert.Equal(PacketFrameResult.Malformed, result);
    }
}