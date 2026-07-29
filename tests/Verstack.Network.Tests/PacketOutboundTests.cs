using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using System.Buffers;

namespace Verstack.Network.Tests;

public sealed class PacketOutboundTests
{
    private readonly FakeNetworkChannel _channel = new FakeNetworkChannel { CompressionThreshold = -1 };
    private readonly IPacketCompressor _compressor = new IdentityCompressor();
    private byte[] _frameScratch = new byte[65536];
    private byte[] _payloadBuffer = new byte[32768];

    private static byte[] CreateTestPayload()
    {
        var w = new ArrayBufferWriter<byte>();
        VarInt.Write(w, 42);
        w.Write(new byte[200]);
        return w.WrittenSpan.ToArray();
    }

    [Fact]
    public void Send_OnePacket_IncreasesWritten()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        outbound.Send(CreateTestPayload());
        Assert.True(outbound.Written > 0);
    }

    [Fact]
    public void Send_ThreePackets_WrittenMatchesThreeFrames()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        var payload = CreateTestPayload();
        outbound.Send(payload);
        int afterFirst = outbound.Written;
        outbound.Send(payload);
        int afterSecond = outbound.Written;
        outbound.Send(payload);
        int afterThird = outbound.Written;

        Assert.True(afterFirst > 0);
        Assert.True(afterSecond > afterFirst);
        Assert.True(afterThird > afterSecond);
    }

    [Fact]
    public void EnableCompression_ChangesThreshold()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        outbound.EnableCompression(256);
        Assert.Equal(256, _channel.CompressionThreshold);
    }
}