using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

internal sealed class NetworkDisconnectSystem(string scope) : IProtoRunSystem
{
    [DI] private readonly NetworkHandoffRouter _handoffRouter = null!;
    [DI] private readonly NetworkHandoffCacheStore _registry = null!;
    
    public void Run()
    {
        var disconnected = _handoffRouter.GetDisconnected(scope);
        while (disconnected.TryDequeue(out var deadChannel))
        {
            int entityId = _registry.GetEntityId(deadChannel);
            
            if (entityId != -1)
            {
                var entity = (ProtoEntity)entityId;
                _registry.DisconnectedStates.Add(entity);
            }
        }
    }
}