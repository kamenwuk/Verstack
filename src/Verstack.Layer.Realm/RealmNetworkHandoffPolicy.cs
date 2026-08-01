using Verstack.Shared.Bridge;
using Leopotam.EcsProto;
using Verstack.Network;

namespace Verstack.Layer.Realm;

public sealed class RealmNetworkHandoffPolicy : BridgeHandoffPolicy
{
    protected override void Init(IProtoSystems systems) { }
    protected override bool TryTransfer(ProtoEntity entity, NetworkChannel channel, out BridgeHandoffData data)
    {
        data = null;
        return false;
    }

    // protected override bool TryTransfer(ProtoEntity entity, NetworkChannel channel)
    // {
    //     return false;
    // }
}