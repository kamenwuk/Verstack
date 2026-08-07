using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input.Movement;

/// <summary>
/// Обрабатывает подтверждение телепортации от клиента.
/// Сервер должен игнорировать пакеты движения, пока не получит этот пакет с нужным ID.
/// </summary>
internal sealed class ConfirmTeleportBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x00) 
            return PacketHandleResult.Ignored;

        var reader = packet.CreateReader();
        
        // Читаем ID телепортации, который мы отправили в Synchronize Player Position (мы писали 0)
        int teleportId = reader.ReadVarInt();

        // Обязательно проверяем валидность прочитанных данных!
        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // TODO: Снять флаг "Ожидание телепортации" в ECS.
        // Например: _world.AddComponent(entity, new TeleportConfirmedEvent(teleportId));

        Logger.Debug(LogKey.PacketPlayTeleportConfirm, (int)entity, teleportId);

        return PacketHandleResult.Accepted;
    }
}