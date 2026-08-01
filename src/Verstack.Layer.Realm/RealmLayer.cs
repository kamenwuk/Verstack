using Verstack.Layer.Realm.User;
using Verstack.Layer.Realm.Join;
using Verstack.Shared.Bridge;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;
using Leopotam.EcsProto;

namespace Verstack.Layer.Realm;

public sealed class RealmLayer : ServerFeatureLayer
{
    public override string Scope => ServerWorldScopes.REALM;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new UserEnterSystem());
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

    protected override BridgeHandoffPolicy GetHandoffPolicy() => new RealmNetworkHandoffPolicy();
}