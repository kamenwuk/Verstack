using Verstack.Network.Lifecycle;
using Verstack.Layer.Global.User;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layer.Gateway;

internal sealed class GatewayCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
    internal readonly ProtoPool<PacketFlowState> FlowStates = null!;
    
    public readonly ProtoItExc ActiveSessionsFilter = new(
        It.Inc<NetworkSession, PacketFlowState>(), 
        It.Exc<NetworkDisconnectedState>()
    );
    
    public readonly ProtoItExc AwaitingHandshakeFilter = new(
        It.Inc<NetworkConnectedState>(), 
        It.Exc<NetworkSession, NetworkDisconnectedState>()
    );
}