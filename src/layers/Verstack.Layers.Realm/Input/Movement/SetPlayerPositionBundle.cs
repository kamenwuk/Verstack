using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Readers;
using Verstack.Engine.Network.Packet;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input.Movement;

/// <summary>
/// Обрабатывает обновление позиции игрока от клиента (без поворота камеры).
/// </summary>
internal sealed class SetPlayerPositionBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x1E) // ID 30 (0x1E) - Set Player Position
            return PacketHandleResult.Ignored;

        var reader = packet.CreateReader();

        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();
        bool onGround = reader.ReadBool();

        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // TODO: Повесить MoveRequestComponent на сущность

        Logger.Debug(LogKey.PacketPlayMove, (int)entity, x, y, z);

        return PacketHandleResult.Accepted;
    }
}