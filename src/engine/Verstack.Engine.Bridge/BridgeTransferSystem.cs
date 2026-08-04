using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Система передачи (Handoff). Проверяет активных игроков на предмет готовности покинуть текущий слой.
/// Запускается самой первой в тике, чтобы передать готовых игроков и очистить их из текущего мира 
/// до того, как отработают игровые системы.
/// </summary>
internal sealed class BridgeTransferSystem(string scope, string nextScope, BridgeHandoffRouter handoffRouter, BridgeHandoffPolicy handoffPolicy) : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _registry = null!;

    public void Init(IProtoSystems systems)
    {
        // Даем политике возможность закэшировать ссылки на свои внутренние пулы
        handoffPolicy?.Init(systems);
    }
        
    public void Run()
    {
        // Если следующий слой не задан — этот слой тупиковый, трансферить некуда
        if (string.IsNullOrEmpty(nextScope))
            return;

        foreach (var entity in _registry.ConnectedFilter)
        {
            var channel = _registry.GetChannel(entity);
            if (channel == null) continue;

            // Делегируем проверку готовности абстрактной политике
            if (handoffPolicy.TryTransfer(entity, channel, out var handoffData))
            {
                // Отправляем канал и посылку (DTO) в следующий ECS-мир
                handoffRouter.TransferToNext(scope, channel, handoffData);
                    
                // Убираем из текущего реестра. Внимание: сокет НЕ закрываем (false), 
                // так как он перешел во владение следующего слоя.
                _registry.Remove(entity, false);
            }
        }
    }
}