using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Модуль интеграции Моста в конкретный ECS-слой. 
/// ВАЖНО: Модуль должен вызываться в самом начале ECS-слоя, до игровых систем.
/// Строго задает порядок систем: сначала уходим (Transfer), потом чистим трупы (Cleanup), 
/// потом принимаем новых (Intake), и в конце фиксируем отключения этого тика (Disconnect).
/// </summary>
public sealed class BridgeLayerModule(string scope, string nextScope, BridgeHandoffRouter handoffRouter, BridgeHandoffPolicy handoffPolicy) : IProtoModule
{
    public void Init(IProtoSystems systems)
    {
        systems.AddSystem(new BridgeTransferSystem(scope, nextScope, handoffRouter, handoffPolicy)) // 1. Трансфер готовых в след. слой
            .AddSystem(new BridgeCleanupSystem())                                              // 2. Очистка трупов и мусора
            .AddSystem(new BridgeIntakeSystem(scope, handoffRouter))                           // 3. Прием новых
            .AddSystem(new BridgeDisconnectSystem(scope, handoffRouter));                     // 4. Пометка отвалившихся в этом тике
    }

    public IProtoAspect[] Aspects()
    {
        return [new BridgeStateCacheStore()];
    }

    public Type[] Dependencies() => [];
}