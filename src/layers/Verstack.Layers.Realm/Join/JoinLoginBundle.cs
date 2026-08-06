using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Global;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// Login (play) (Clientbound, 0x31): отправляется сервером при входе игрока. Сеет базовое
/// состояние: Entity ID игрока, игровой режим, тип измерения, сид мира, уровень моря и т.д.
/// </summary>
internal sealed class JoinLoginBundle : PacketBundle
{
    public override int StepCount => 1;
    
    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        // Ожидаем пакет Login Acknowledged (ID 0x03) от клиента. 
        // В современных версиях клиент присылает его последним в стадии Login, подтверждая вход.
        if (packet.Id != 0x03) 
            return PacketHandleResult.Ignored; 
        
        var writer = outbound.Begin();

        bool hasDeathLocation = false;
        
        writer.WriteVarInt(0x31) // Clientbound Login (Play) — Идентификатор пакета входа в стадию Play.
            .WriteInt((int)entity + 1) // Entity ID (Int) — Уникальный ID сущности игрока. Берём из ECS и прибавляем 1.
            .WriteBool(false) // Is hardcore — Режим хардкора (false = отключен).
            .WriteVarInt(1) // Dimension Names length — Количество передаваемых измерений (1 шт).
            .WriteString(SpawnConstants.DIMENSION_NAME) // Dimension Names — единственное передаваемое измерение.
            .WriteVarInt(20) // Max Players — Игнорируется клиентом, но нужно для совместимости.
            .WriteVarInt(WorldConstants.VIEW_DISTANCE) // View Distance — Дальность прорисовки чанков клиентом.
            .WriteVarInt(WorldConstants.SIMULATION_DISTANCE) // Simulation Distance — Дальность симуляции на сервере.
            .WriteBool(false) // Reduced Debug Info — Скрывать ли расширенную информацию на F3.
            .WriteBool(true) // Enable respawn screen — Показывать ли экран смерти.
            .WriteBool(false) // Do limited crafting — Ограничение крафта (клиентом не используется, но нужно передать).
            .WriteVarInt(SpawnConstants.DIMENSION_TYPE_ID) // Dimension Type — ID типа измерения из реестра.
            .WriteString(SpawnConstants.DIMENSION_NAME) // Dimension Name — Имя измерения, в которое спавнится игрок.
            .WriteLong(0L) // Hashed seed — Первые 8 байт SHA-256 хэша сида мира (для шума биомов).
            .WriteByte(WorldConstants.GAME_MODE) // Game mode — Текущий игровой режим игрока.
            .WriteByte(unchecked((byte)WorldConstants.PREVIOUS_GAME_MODE)) // Previous Game mode — -1 = не определён.
            .WriteBool(false) // Is Debug — Debug-мир (блоки нельзя ломать/ставить).
            .WriteBool(false) // Is Flat — Superflat (влияет на туман и горизонт).
            .WriteBool(hasDeathLocation); // Has death location — Есть ли точка смерти для компаса.
        
        // Если hasDeathLocation = true, клиент будет ожидать эти два поля:
        if (hasDeathLocation)
        {
            writer.WriteString(""); // Death dimension name (Optional Identifier).
            writer.WriteVector3(0, 0, 0); // Death location (Optional Position).
        }
        
        writer.WriteVarInt(300) // Portal cooldown — Тики до повторного использования портала (300 = 15 сек).
            .WriteVarInt(63) // Sea level — Уровень моря (Y=63).
            .WriteBool(false) // Online mode — Онлайн-режим (скины, профили).
            .WriteBool(false); // Enforces Secure Chat — Проверка подписей в чате.
        
        outbound.Commit(ref writer);
        
        Logger.Debug(LogKey.PacketPlayLogin, (int)entity);

        return PacketHandleResult.Continue;
    }
}