using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;

namespace Verstack.Layer.Gateway;

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
        scopes.Add(ServerWorldScopes.REALM);
    }
}