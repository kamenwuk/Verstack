using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Writers;
using Verstack.Engine.Network.Packet;
using Verstack.Layers.Global;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// Synchronize Player Position (Clientbound, 0x48): телепортирует игрока в точку входа.
/// В версии 26.2+ поля Velocity добавлены, а Flags изменены с Byte на Int.
/// </summary>
internal sealed class JoinTeleportBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        var writer = outbound.Begin();

        writer.WriteVarInt(0x48) // Synchronize Player Position
            .WriteVarInt(0) // Teleport ID
            .WriteDouble(SpawnConstants.SPAWN_BLOCK_X) // X — позиция стоп.
            .WriteDouble(SpawnConstants.SPAWN_BLOCK_Y + 1.0) // Y — стопы на блок выше поверхности (камень Y=64 → стопы 65).
            .WriteDouble(SpawnConstants.SPAWN_BLOCK_Z) // Z
            .WriteDouble(0.0) // Velocity X
            .WriteDouble(0.0) // Velocity Y
            .WriteDouble(0.0) // Velocity Z
            .WriteFloat(SpawnConstants.SPAWN_YAW) // Yaw
            .WriteFloat(SpawnConstants.SPAWN_PITCH) // Pitch
            .WriteInt(0); // Flags — все абсолютные.
        
        outbound.Commit(ref writer);

        Logger.Debug(LogKey.PacketPlayPosition, (int)entity);

        return PacketHandleResult.Accepted;
    }
}