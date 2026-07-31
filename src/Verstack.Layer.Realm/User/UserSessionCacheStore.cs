using System.Buffers;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Layer.Global.User;
using Verstack.Layer.Realm.Join;
using Verstack.Nbt;
using Verstack.Network;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Realm.User;

public sealed class UserSessionCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
    internal readonly ProtoPool<EnterPendingTag> EnterPending = null!;
    
    private readonly Dictionary<int, NetworkChannel> _entityToChannel = new();
    private readonly Dictionary<NetworkChannel, int> _channelToEntity = new();
    
    internal NetworkChannel GetChannel(int entityId) => _entityToChannel.GetValueOrDefault(entityId, null);
    
    internal void AddChannel(int entityId, NetworkChannel channel)
    {
        _entityToChannel[entityId] = channel;
        _channelToEntity[channel] = entityId;
    }
    
    internal int RemoveChannel(NetworkChannel channel)
    {
        if (!_channelToEntity.Remove(channel, out int entityId)) return -1;
        _entityToChannel.Remove(entityId);
        return entityId;
    }

    public void Transfer(UserProfile user, NetworkSession session, NetworkChannel channel)
    {
        UserProfiles.NewEntity(out var entity) = user;
        Sessions.Add(entity) = session;
        AddChannel((int)entity, channel);

        EnterPending.Add(entity);
    }
}