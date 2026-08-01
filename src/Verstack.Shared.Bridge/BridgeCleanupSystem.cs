using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Shared.Bridge;

/// <summary>
/// Система очистки. Выполняется на каждом тике для удаления "мусорных" и отключенных сущностей.
/// Важно: запускается до системы приема (Intake), чтобы в текущем тике новые сущности 
/// не смешивались с теми, кто уже помечен на удаление.
/// </summary>
internal sealed class BridgeCleanupSystem : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _registry = null!;
    [DI] private readonly ProtoWorld _world = null!;
        
    public void Run()
    {
        // 1. Удаляем "мусор": сущности, которые были созданы в состоянии ожидания (Pending),
        // но за прошедший тик их никто не забрал из очереди (например, игровой слой не успел их обработать),
        // либо они отвалились по таймауту, так и не успев войти в игру.
        foreach (var entity in _registry.PendingGarbageFilter)
        {
            _registry.RemoveChannel(entity, true);
            _world.DelEntity(entity);
        }
            
        // 2. Удаляем сущности, которые уже были активно задействованы в слое (находились на "рельсах"),
        // но потеряли сетевое соединение.
        foreach (var entity in _registry.DisconnectedFilter)
        {
            _registry.RemoveChannel(entity, true);
            _world.DelEntity(entity);
        }
    }
}