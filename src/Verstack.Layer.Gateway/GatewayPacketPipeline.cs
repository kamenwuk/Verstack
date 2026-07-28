using Verstack.Network.Packet;
using System.Buffers;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Layer.Gateway.Bundles;

namespace Verstack.Layer.Gateway;

internal sealed class GatewayPacketPipeline : IProtoInitService
{
    private PacketPipeline _pipeline = null!;

    public void Init(IProtoSystems systems)
    {
        _pipeline = new PacketPipeline(systems, [
            new StatusExchangeBundle(),
            new PingPongBundle()
        ]);
    }
    
    /// <summary>
    /// Запускает пакет через текущий бандл.
    /// </summary>
    public bool TryProcessPacket(in ProtoEntity entity, in RawPacket packet, IBufferWriter<byte> writer, ref PacketFlowState state)
    {
        return _pipeline.TryProcessPacket(entity, packet, writer, ref state);
    }
}