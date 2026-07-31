using Verstack.Network.Lifecycle;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Network;
using Verstack.Debug;

namespace Verstack.Layer.Gateway;

public sealed class GatewayNetworkHandoffPolicy : NetworkHandoffPolicy
{
    private GatewayCacheStore _gatewayCache = null!;
    
    protected override void Init(IProtoSystems systems)
    {
        var world = systems.World();
        _gatewayCache = world.Aspect<GatewayCacheStore>();
    }

    // TODO: Временно не работает поправится в следующей итерации
    protected override bool TryTransfer(ProtoEntity entity, NetworkChannel channel)
    {
        // 1. Быстрая проверка: есть ли вообще профиль и сессия
        if (!_gatewayCache.UserProfiles.Has(entity) || !_gatewayCache.Sessions.Has(entity))
            return false;

        // 2. Проверка этапа: дошел ли игрок до конца пайплайна (например, ConfigurationFinish)
        ref var flowState = ref _gatewayCache.FlowStates.Get(entity);
        if (flowState.BundleIndex < 6) // 6 = индекс ConfigurationFinishBundle
            return false;

        ref readonly var user = ref _gatewayCache.UserProfiles.Get(entity);
        ref readonly var session = ref _gatewayCache.Sessions.Get(entity);
    
        Logger.Info(LogKey.PacketRealmTransfer, user.Username);
        
        return true; 
    }
}