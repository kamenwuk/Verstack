using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway;

public sealed class GatewayLayer : ServerFeatureLayer
{
    public override string Scope => ServerWorldScopes.GATEWAY;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new GuestScreeningSystem())
            .AddSystem(new PacketDispatchSystem());
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return
        [
            new GatewayCacheStore()
        ];
    }

    protected override void GetVisibleScopes(ICollection<string> scopes)
    {
        scopes.Add(ServerWorldScopes.GLOBAL);
    }

    protected override string GetNextScope() => ServerWorldScopes.REALM;
    
    protected override BridgeHandoffPolicy GetHandoffPolicy() => new GatewayHandoffPolicy();
}