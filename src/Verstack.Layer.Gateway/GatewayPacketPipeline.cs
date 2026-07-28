using Verstack.Layer.Gateway.Bundles;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layer.Gateway;

internal sealed class GatewayPacketPipeline : IProtoInitService
{
    private PacketPipeline _pipeline = null!;

    /// <summary>
    /// Число бандлов в конвейере. PacketDispatchSystem использует это, чтобы определить
    /// выход за пределы фазы (Login Acknowledged → завершение конвейера → закрытие канала).
    /// </summary>
    public int BundleCount => _pipeline.BundleCount;
    
    public void Init(IProtoSystems systems)
    {
        _pipeline = new PacketPipeline(systems, [
            new StatusExchangeBundle(),
            new PingPongBundle(),
            new LoginStartBundle(),          // ← индекс 2
            new LoginAcknowledgedBundle()
        ]);
    }
    
    /// <summary>
    /// Запускает пакет через текущий бандл.
    /// </summary>
    public bool TryProcessPacket(in ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound, ref PacketFlowState state)
    {
        return _pipeline.TryProcessPacket(entity, packet, ref outbound, ref state);
    }
}