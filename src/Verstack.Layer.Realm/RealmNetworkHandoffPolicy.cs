using Verstack.Network.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Network;

namespace Verstack.Layer.Realm;

public sealed class RealmNetworkHandoffPolicy : NetworkHandoffPolicy
{
    protected override void Init(IProtoSystems systems) { }

    protected override bool TryTransfer(ProtoEntity entity, NetworkChannel channel)
    {
        return false;
    }
}