using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Layers.Global.User;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Shared;

internal sealed class UserSessionCacheStore : ProtoAspectInject
{
    internal ProtoItExc ItPlaying { get; private set; } = null!;
    internal ProtoItExc ItJoining { get; private set; } = null!;
    internal ProtoItExc ItLeaving { get; private set; } = null!;
    
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
    internal readonly ProtoPool<PacketFlowState> FlowStates = null!;

    public override void Init(ProtoWorld world)
    {
        var bridgeStateCacheStore = world.Aspect<BridgeStateCacheStore>();
        {
            var incMask = new List<Type>(bridgeStateCacheStore.ConnectedFilter.IncTypes)
            {
                typeof(NetworkSession),
                typeof(UserProfile)
            };
            var excMask = new List<Type>(bridgeStateCacheStore.ConnectedFilter.ExcTypes)
            {
                typeof(PacketFlowState)
            };
            ItPlaying = new ProtoItExc(incMask.ToArray(), excMask.ToArray());
        }

        {
            var incMask = new List<Type>(bridgeStateCacheStore.ConnectedFilter.IncTypes)
            {
                typeof(NetworkSession),
                typeof(UserProfile),
                typeof(PacketFlowState)
            };
            var excMask = new List<Type>(bridgeStateCacheStore.ConnectedFilter.ExcTypes);
            ItJoining = new ProtoItExc(incMask.ToArray(), excMask.ToArray());
        }

        {
            var incMask = new List<Type>(bridgeStateCacheStore.DisconnectedFilter.IncTypes)
            {
                typeof(NetworkSession),
                typeof(UserProfile)
            };
            var excMask = new List<Type>(bridgeStateCacheStore.DisconnectedFilter.ExcTypes)
            {
                typeof(PacketFlowState)
            };
            ItLeaving = new ProtoItExc(incMask.ToArray(), excMask.ToArray());
        }
        base.Init(world);
    }
}