using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Система приема. Читает новые подключения (или трансферы из предыдущего слоя) из маршрутизатора
/// и создает для них ECS-сущности в состоянии ожидания (Pending).
/// Запускается после системы очистки (Cleanup).
/// </summary>
internal sealed class BridgeIntakeSystem(string scope, BridgeHandoffRouter handoffRouter) : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _registry = null!;
        
    public void Run()
    {
        while (handoffRouter.TryDequeuePending(scope, out var channel, out var handoffData))
        {
            // Защита от гонки (Race Condition): если клиент подключился и мгновенно отвалился,
            // пока лежал в очереди маршрутизатора. Такую сущность создавать не нужно.
            if (channel.IsDisconnected)
                continue;

            _registry.RegisterPending(channel, handoffData);
        }
    }
}