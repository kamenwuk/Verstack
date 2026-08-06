using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Layers.Global.User;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layers.Realm.Movement;

namespace Verstack.Layers.Realm.User;

internal sealed class UserSessionCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
    internal readonly ProtoPool<PacketFlowState> FlowStates = null!;
    internal readonly ProtoPool<MoveReq> MoveReqs = null!;   // ← добавить
}