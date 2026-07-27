using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Core;

namespace Verstack.Layer.Realm;

public sealed class RealmFeature : VerstackFeature
{
    public override string Scope => WorldScopes.REALM;

    public override void Init(IProtoSystems systems)
    {
        
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return [];
    }
}