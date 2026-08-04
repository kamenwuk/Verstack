using Verstack.Layers.Realm.Input;
using Verstack.Layers.Realm.User;
using Verstack.Layers.Realm.Join;
using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm;

public sealed class RealmLayer : ServerFeatureLayer
{
    public override string Scope => ServerWorldScopes.REALM;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new HandoffApprovalSystem())
            .AddSystem(new InboundDispatcherSystem());
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return
        [
            new UserSessionCacheStore()
        ];
    }

    protected override void GetVisibleScopes(ICollection<string> scopes)
    {
        scopes.Add(ServerWorldScopes.GLOBAL);
    }

    protected override string GetNextScope() => string.Empty;

    protected override BridgeHandoffPolicy GetHandoffPolicy() => null;
}