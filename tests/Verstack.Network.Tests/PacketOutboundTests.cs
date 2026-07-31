using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using Verstack.Network.Packet;

namespace Verstack.Network.Tests;

public sealed class PacketOutboundTests
{
    private readonly FakeNetworkChannel _channel = new FakeNetworkChannel { CompressionThreshold = -1 };
    private readonly IPacketCompressor _compressor = new IdentityCompressor();
    private byte[] _frameScratch = new byte[65536];
    private byte[] _payloadBuffer = new byte[32768];

    [Fact]
    public void Commit_OnePacket_SendsDataOnFlush()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        outbound.Commit(ref writer);
        
        // Если Commit не сработал или забыл сбросить writer, Flush бросит исключение в DEBUG
        outbound.Flush();

        // Если у тестового проекта есть доступ к internal-очереди:
        Assert.False(_channel.OutboundQueue.IsEmpty);
    }

    [Fact]
    public void Commit_ThreePackets_AccumulatesInChannel()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        
        // Пакет 1
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        outbound.Commit(ref writer);
        
        // Пакет 2
        writer = outbound.Begin();
        writer.WriteVarInt(42);
        outbound.Commit(ref writer);
        
        // Пакет 3
        writer = outbound.Begin();
        writer.WriteVarInt(42);
        outbound.Commit(ref writer);
        
        // Flush отправляет всё разом
        outbound.Flush();

        Assert.False(_channel.OutboundQueue.IsEmpty);
    }

    [Fact]
    public void EnableCompression_ChangesThreshold()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        outbound.EnableCompression(256);
        Assert.Equal(256, _channel.CompressionThreshold);
    }

    [Fact]
    public void Flush_WithoutCommit_ThrowsInDebug()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        
        // Намеренно НЕ вызываем outbound.Commit(ref writer);
        
#if DEBUG
        // ИСПОЛЬЗУЕМ try/catch ВМЕСТО ЛЯМБДЫ, так как PacketOutbound — это ref struct
        bool threw = false;
        try
        {
            outbound.Flush();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw, "Ожидалось исключение InvalidOperationException в DEBUG-режиме.");
#endif
    }

    [Fact]
    public void Begin_TwiceWithoutCommit_ThrowsInDebug()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        
        // Намеренно НЕ вызываем outbound.Commit(ref writer);
        
#if DEBUG
        // ИСПОЛЬЗУЕМ try/catch ВМЕСТО ЛЯМБДЫ
        bool threw = false;
        try
        {
            outbound.Begin();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw, "Ожидалось исключение InvalidOperationException в DEBUG-режиме.");
#endif
    }
}