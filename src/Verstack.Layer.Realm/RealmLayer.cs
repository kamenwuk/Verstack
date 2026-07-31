using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layer.Realm.Systems;
using Verstack.Layer.Realm.User;
using Verstack.Lifecycle;

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
}