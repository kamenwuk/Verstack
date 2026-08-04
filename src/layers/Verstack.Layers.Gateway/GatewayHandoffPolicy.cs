using Verstack.Engine.Network;
using Verstack.Engine.Bridge;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layers.Global;

namespace Verstack.Layers.Gateway;

public sealed class GatewayHandoffPolicy : BridgeHandoffPolicy
{
    private GatewayCacheStore _gatewayCache = null!;
    
    protected override void Init(IProtoSystems systems)
    {
        var world = systems.World();
        _gatewayCache = world.Aspect<GatewayCacheStore>();
    }

    protected override bool TryTransfer(ProtoEntity entity, NetworkChannel channel, out BridgeHandoffData data)
    {
        if (!_gatewayCache.UserProfiles.Has(entity) || !_gatewayCache.Sessions.Has(entity))
        {
            data = null!;
            return false;
        }
        
        ref var flowState = ref _gatewayCache.FlowStates.Get(entity);
        if (flowState.BundleIndex < 6) 
        {
            data = null!;
            return false;
        }

        var profile = _gatewayCache.UserProfiles.Get(entity);
        var session = _gatewayCache.Sessions.Get(entity);
    
        Logger.Info(LogKey.PacketRealmTransfer, profile.Username);
        
        // Упаковываем
        data = new EnterRealmHandoffData(profile, session);
        return true; 
    }
}