using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Network;

namespace Verstack.Shared.Bridge;

/// <summary>
/// Аспект (CacheStore), выделяемый на каждый слой, где будет работа с сетью и переход.
/// Инкапсулирует конечный автомат состояний игрока на уровне этого слоя.
/// Управляет переходами: Pending -> Connected -> Disconnected.
/// Скрывает пулы компонентов от внешних систем, предоставляя только безопасные методы и фильтры.
/// </summary>
public sealed class BridgeStateCacheStore : ProtoAspectInject
{
    /// <summary>
    /// Для игровых систем слоя: активные игроки, готовые к обработке.
    /// <remarks>Виден, пока на сущности нет BridgeClientDisconnected и BridgeHandoffPending.</remarks>
    /// </summary>
    public readonly ProtoItExc ConnectedFilter = new(It.Inc<BridgeClientConnected>(), It.Exc<BridgeClientDisconnected>());
    
    /// <summary>
    /// Для системы очистки: активные игроки, потерявшие соединение.
    /// <remarks>Виден, но на сущности не должно быть BridgeHandoffPending.</remarks>
    /// </summary>
    public readonly ProtoItExc DisconnectedFilter = new(It.Inc<BridgeClientDisconnected>(), It.Exc<BridgeHandoffPending>());
    
    /// <summary>
    /// Для системы очистки: игроки, зависшие в промежуточном состоянии (мусор).
    /// </summary>
    internal readonly ProtoIt PendingGarbageFilter = new(It.Inc<BridgeHandoffPending>());
    
    private readonly ProtoPool<BridgeClientDisconnected> _disconnectedStates = null!;
    private readonly ProtoPool<BridgeClientConnected> _connectedStates = null!;
    private readonly ProtoPool<BridgeHandoffPending> _pendingStates = null!;
    
    // Маппинги для быстрого поиска канала по сущности и наоборот
    private readonly Dictionary<ProtoEntity, NetworkChannel> _entityToChannel = new();
    private readonly Dictionary<NetworkChannel, ProtoEntity> _channelToEntity = new();
    
    // FIFO-очередь данных, ожидающих "распаковки" специфичными системами слоя
    private readonly Queue<HandoffPayload> _handoffQueue = new();

    /// <summary>
    /// Создает сущность в состоянии Pending и регистрирует её в очереди на распаковку.
    /// </summary>
    internal void RegisterPending(NetworkChannel channel, BridgeHandoffData data)
    {
        _pendingStates.NewEntity(out var entity);
        _handoffQueue.Enqueue(new HandoffPayload(entity, data));
        
        _entityToChannel[entity] = channel;
        _channelToEntity[channel] = entity;
    }

    /// <summary>
    /// Вызов для игровых систем слоя (например, системы логина). 
    /// Извлекает данные из очереди и переводит сущность из Pending в Connected (на "рельсы").
    /// </summary>
    public bool TryDequeueHandoff(out HandoffPayload payload)
    {
        while (_handoffQueue.TryDequeue(out payload))
        {
            // Если игрок отключился, пока висел в очереди — пропускаем его (будет удален как мусор).
            if (_disconnectedStates.Has(payload.Entity) || !_entityToChannel.ContainsKey(payload.Entity))
                continue;

            // Перевод в активное состояние
            _connectedStates.Add(payload.Entity);
            _pendingStates.Del(payload.Entity);
            
            return true;
        }

        payload = default;
        return false;
    }

    public ProtoEntity GetEntity(NetworkChannel channel) => _channelToEntity.GetValueOrDefault(channel, default);
    public NetworkChannel GetChannel(ProtoEntity entity) => _entityToChannel.GetValueOrDefault(entity, null);

    /// <summary>
    /// Помечает сущность как отключенную по сигналу из TCP-потока.
    /// </summary>
    internal void MarkDisconnected(NetworkChannel channel)
    {
        if (_channelToEntity.TryGetValue(channel, out var entity))
        {
            _disconnectedStates.Add(entity);
        }
    }
    
    /// <summary>
    /// Полное удаление сущности из кэша и очистка её ECS-компонентов.
    /// </summary>
    /// <param name="closeSocket">Закрывать ли сырой TCP-сокет. True при очистке, False при трансфере в другой слой.</param>
    internal void RemoveChannel(ProtoEntity entity, bool closeSocket)
    {
        if (!_entityToChannel.Remove(entity, out var channel))
            return;
        
        _channelToEntity.Remove(channel);
        
        // Снимаем все возможные компоненты состояний
        if(_pendingStates.Has(entity))
            _pendingStates.Del(entity);
    
        if(_disconnectedStates.Has(entity))
            _disconnectedStates.Del(entity);
        
        _connectedStates.Del(entity);
    
        if (closeSocket)
            channel?.Disconnect();
    }
}