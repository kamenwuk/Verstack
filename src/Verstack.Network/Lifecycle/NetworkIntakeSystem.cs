using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

internal sealed class NetworkIntakeSystem(string scope) : IProtoRunSystem
{
    [DI] private readonly NetworkHandoffCacheStore _registry = null!;
    [DI] private readonly NetworkHandoffRouter _handoffRouter = null!;
    
    public void Run()
    {
        var pending = _handoffRouter.GetPending(scope);
        while (pending.TryDequeue(out var channel))
        {
            // Создаем сущность в текущем мире
            _registry.ConnectedStates.NewEntity(out var entity);
            
            // Регистрируем маппинг
            _registry.Register((int)entity, channel);
        }
    }
}