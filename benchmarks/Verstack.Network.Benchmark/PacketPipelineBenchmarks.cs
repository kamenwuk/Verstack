using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using Leopotam.EcsProto;
using Verstack.Network;
using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

[ShortRunJob]
[MemoryDiagnoser]
public class PacketPipelineBenchmarks
{
    private PacketPipeline _pipeline;
    private RawPacket _rawPacket;
    private FakeNetworkChannel _channel;
    private IPacketCompressor _compressor;
    private byte[] _frameScratch;
    private byte[] _payloadBuffer;
    private ProtoEntity _dummyEntity;

    [GlobalSetup]
    public void Setup()
    {
        var bundles = new PacketBundle[] { new DummyBundle() };
        _pipeline = new PacketPipeline(null, bundles);
        _rawPacket = new RawPacket(42, new byte[100]);

        _frameScratch = new byte[65536];
        _payloadBuffer = new byte[32768];
        _compressor = new IdentityCompressor();
        _channel = new FakeNetworkChannel();
        _dummyEntity = default;
    }

    [Benchmark]
    public bool ProcessPacket()
    {
        var state = new PacketFlowState(0, 0);
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        return _pipeline.TryProcessPacket(_dummyEntity, _rawPacket, ref outbound, ref state);
    }
}

internal class DummyBundle : PacketBundle
{
    public override int StepCount => 1;
    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
        => PacketHandleResult.Accepted;
}