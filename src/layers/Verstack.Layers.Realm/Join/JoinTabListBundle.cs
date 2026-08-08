using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Realm.Shared;
using Verstack.Layers.Global;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layers.Realm.Session.World;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// player_info_update (Clientbound, 0x46): добавляет/обновляет игроков в TAB-листе.
/// При входе добавляет самого игрока.
/// </summary>
internal sealed class JoinTabListBundle : PacketBundle
{
    private UserSessionCacheStore _userSessionCacheStore;
    public override int StepCount => 1;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.World();
        _userSessionCacheStore = world.Aspect<UserSessionCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        ref readonly var userProfile = ref _userSessionCacheStore.UserProfiles.Get(entity);

        var writer = outbound.Begin();
        WorldObjectWriters.WriteUserInfoAdd(ref writer, userProfile.Uuid, userProfile.Username);
        outbound.Commit(ref writer);

        Logger.Debug(LogKey.PacketPlayInfoUpdate, (int)entity);

        return PacketHandleResult.Continue;
    }
}