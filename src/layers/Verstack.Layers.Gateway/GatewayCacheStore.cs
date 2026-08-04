using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Layers.Global.User;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway;

internal sealed class GatewayCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
    internal readonly ProtoPool<PacketFlowState> FlowStates = null!;

    public readonly ProtoIt ActiveSessionsFilter = new(It.Inc<NetworkSession, PacketFlowState>());
}