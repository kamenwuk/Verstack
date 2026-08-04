using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Readers;
using Verstack.Engine.Network.Packet;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input.Movement;

/// <summary>
/// Обрабатывает обновление позиции и поворота игрока от клиента.
/// </summary>
internal sealed class SetPlayerPositionAndRotationBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x1F) // ID 31 (0x1F) - Set Player Position and Rotation
            return PacketHandleResult.Ignored;

        var reader = packet.CreateReader();

        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();
        float yaw = reader.ReadFloat();   // Поворот по горизонтали
        float pitch = reader.ReadFloat(); // Наклон взгляда
        bool onGround = reader.ReadBool();

        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // TODO: Повесить MoveRequestComponent (с yaw/pitch) на сущность

        // Можно использовать тот же ключ лога или создать новый для ротации
        Logger.Debug(LogKey.PacketPlayMove, (int)entity, x, y, z);

        return PacketHandleResult.Accepted;
    }
}