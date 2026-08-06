using Verstack.Engine.Network.Packet.Outbound;
using BenchmarkDotNet.Attributes;

namespace Verstack.Engine.Network.Benchmark;

[MemoryDiagnoser]
public class PacketWriterBenchmarks
{
    private byte[] _payloadBuffer = null!;
    private string _minecraftOverworldString = null!;
    private ReadOnlySpan<byte> _minecraftOverworldUtf8 => "minecraft:overworld"u8;
    private Guid _testUuid;

    [GlobalSetup]
    public void Setup()
    {
        _payloadBuffer = new byte[256];
        _minecraftOverworldString = "minecraft:overworld";
        _testUuid = Guid.NewGuid();
    }

    [Benchmark(Baseline = true)]
    public int WriteVarInt()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        writer.WriteVarInt(2147483647);
        return writer.Written;
    }

    [Benchmark]
    public int WriteString_FromUtf8Span()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        writer.WriteString(_minecraftOverworldUtf8);
        return writer.Written;
    }

    [Benchmark]
    public int WriteString_FromCSharpString()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        writer.WriteString(_minecraftOverworldString);
        return writer.Written;
    }

    [Benchmark]
    public int WriteUuid()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        writer.WriteUuid(_testUuid);
        return writer.Written;
    }

    [Benchmark]
    public int WriteVector3()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        writer.WriteVector3(10, 64, -20);
        return writer.Written;
    }

    [Benchmark]
    public int AssemblePlayLoginPacket()
    {
        var writer = new PacketStreamWriter(_payloadBuffer);
        
        writer.WriteVarInt(0x31)
              .WriteInt(1)
              .WriteBool(false)
              .WriteVarInt(1)
              .WriteString(_minecraftOverworldUtf8)
              .WriteVarInt(20)
              .WriteVarInt(10)
              .WriteVarInt(10)
              .WriteBool(false)
              .WriteBool(true)
              .WriteBool(false)
              .WriteVarInt(0)
              .WriteString(_minecraftOverworldUtf8)
              .WriteLong(0)
              .WriteByte(1)
              .WriteByte(0xFF)
              .WriteBool(false)
              .WriteBool(false)
              .WriteBool(false)
              .WriteVarInt(0)
              .WriteVarInt(63)
              .WriteBool(false)
              .WriteBool(false);

        return writer.Written;
    }
}