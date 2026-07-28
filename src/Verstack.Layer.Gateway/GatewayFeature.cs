using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Core;

namespace Verstack.Layer.Gateway;

public sealed class GatewayFeature : VerstackFeature
{
    public override string Scope => WorldScopes.GATEWAY;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new GuestScreeningSystem())
            .AddSystem(new PacketDispatchSystem())
            .AddService(new GatewayPacketPipeline())
            .InitHere<GatewayPacketPipeline>();
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return
        [
            new GatewayCacheStore()
        ];
    }
}