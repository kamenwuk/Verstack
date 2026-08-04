using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Writers;
using Verstack.Engine.Network.Packet;
using Verstack.Layers.Realm.User;
using Verstack.Layers.Global;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

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
        var writer = outbound.Begin();

        ref readonly var userProfile = ref _userSessionCacheStore.UserProfiles.Get(entity);
        
        Guid playerUuid = userProfile.Uuid;

        writer.WriteVarInt(0x46) // player_info_update — обновление инфо об игроке.
            // Битовая маска действий 0x1D: Add Player(0x01) + Game Mode(0x04) + Listed(0x08) + Latency(0x10).
            .WriteByte(0x1D) // Actions — флаги отправляемых полей.
            .WriteVarInt(1) // Players length — количество игроков (1).
            .WriteUuid(playerUuid); // UUID — обновляемый игрок.

        // --- 0x01: Add Player ---
        writer.WriteString(userProfile.Username); // Name (String 16) — имя игрока.
        writer.WriteVarInt(0); // Properties length — свойства профиля (скины). 0 = офлайн-режим.

        // --- 0x02: Initialize Chat — пропущено (бит 0x02 не установлен). ---

        // --- 0x04: Update Game Mode ---
        writer.WriteVarInt(WorldConstants.GAME_MODE); // Game Mode — игровой режим игрока.

        // --- 0x08: Update Listed ---
        writer.WriteBool(true); // Listed — отображать в таб-листе.

        // --- 0x10: Update Latency ---
        writer.WriteVarInt(0); // Ping — пинг игрока (0 = идеальный).

        // --- 0x20/0x40/0x80: Display/Priority/Hat — пропущены. ---

        outbound.Commit(ref writer);

        Logger.Debug(LogKey.PacketPlayInfoUpdate, (int)entity);

        return PacketHandleResult.Continue;
    }
}