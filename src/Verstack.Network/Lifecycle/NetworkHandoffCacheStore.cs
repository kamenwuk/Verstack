using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

public sealed class NetworkHandoffCacheStore : ProtoAspectInject
{
    public readonly ProtoIt DisconnectedFilter = new(It.Inc<NetworkDisconnectedState>());
    public readonly ProtoItExc ConnectedFilter = new(It.Inc<NetworkConnectedState>(), It.Exc<NetworkDisconnectedState>());
    
    /// <summary>Пул сущностей, помеченных как отключённые (ожидают очистки).</summary>
    internal readonly ProtoPool<NetworkDisconnectedState> DisconnectedStates = null!;

    /// <summary>Пул сущностей с активным сетевым соединением.</summary>
    internal readonly ProtoPool<NetworkConnectedState> ConnectedStates = null!;
    
    private readonly Dictionary<int, NetworkChannel> _entityToChannel = new();
    private readonly Dictionary<NetworkChannel, int> _channelToEntity = new();

    internal void Register(int entityId, NetworkChannel channel)
    {
        _entityToChannel[entityId] = channel;
        _channelToEntity[channel] = entityId;
    }

    public int GetEntityId(NetworkChannel channel) => 
        _channelToEntity.GetValueOrDefault(channel, -1);

    public NetworkChannel GetChannel(int entityId) => 
        _entityToChannel.GetValueOrDefault(entityId, null);

    internal void RemoveChannel(ProtoEntity entity, bool closeSocket)
    {
        if (!_entityToChannel.Remove((int)entity, out var channel))
            return;
        
        _channelToEntity.Remove(channel);
        
        if (closeSocket)
            channel?.Disconnect();
    }
}