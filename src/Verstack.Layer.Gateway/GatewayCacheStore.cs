using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Network;

namespace Verstack.Layer.Gateway;

public sealed class GatewayCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<PacketFlowState> FlowStates = null!;
    
    // Прямой словарь: Сущность -> Канал
    private readonly Dictionary<int, NetworkChannel> _entityToChannel = new();
    // Обратный словарь: Канал -> Сущность (для быстрого удаления)
    private readonly Dictionary<NetworkChannel, int> _channelToEntity = new();
    
    internal void AddChannel(int entityId, NetworkChannel channel)
    {
        _entityToChannel[entityId] = channel;
        _channelToEntity[channel] = entityId;
    }

    internal NetworkChannel GetChannel(int entityId) => 
        _entityToChannel.GetValueOrDefault(entityId, null);

    // Возвращает ID сущности и удаляет связь
    internal int RemoveChannel(NetworkChannel channel)
    {
        if (!_channelToEntity.Remove(channel, out int entityId)) 
            return -1;
        
        _entityToChannel.Remove(entityId);
        return entityId;
    }
}