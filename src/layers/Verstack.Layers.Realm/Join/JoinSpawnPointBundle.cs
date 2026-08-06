using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Global;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// set_default_spawn_position (Clientbound, 0x61): отправляется сервером после логина,
/// чтобы указать координаты точки возрождения (куда указывает компас).
/// </summary>
internal sealed class JoinSpawnPointBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        var writer = outbound.Begin();

        writer.WriteVarInt(0x61) // set_default_spawn_position — точка спавна (для компаса).
            .WriteString(SpawnConstants.DIMENSION_NAME) // Dimension Name — измерение точки спавна.
            .WriteVector3(SpawnConstants.SPAWN_BLOCK_X, SpawnConstants.SPAWN_BLOCK_Y, SpawnConstants.SPAWN_BLOCK_Z) // Location — блок спавна (Long: X/Z по 26 бит, Y 12 бит).
            .WriteFloat(SpawnConstants.SPAWN_YAW) // Yaw — рыскание после возрождения (0 = юг).
            .WriteFloat(SpawnConstants.SPAWN_PITCH); // Pitch — тангаж после возрождения (0 = прямо).
        
        outbound.Commit(ref writer);

        Logger.Debug(LogKey.PacketPlaySpawnPosition, (int)entity);

        return PacketHandleResult.Continue;
    }
}