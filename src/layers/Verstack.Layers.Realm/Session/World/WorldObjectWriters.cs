using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Layers.Global;
using Verstack.Shared.Maths;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Прямая запись клиентблаунд-пакетов репликации объектов мира. Без состояний: вызывающий
/// делает Begin/Commit на своей <see cref="OutboundLease"/>. Entity ID выводится из
/// ECS-сущности как <c>(int)entity + 1</c> (тот же принцип, что в JoinLoginBundle 0x31).
///
/// <para>Spawn Entity (0x01) — общий для всех типов: отличается только полем Type (значение
/// <see cref="WorldObjectKind"/>) и UUID. Player-specific пакеты (<c>player_info</c>)
/// вынесены отдельно — они нужны только игрокам.</para>
/// </summary>
internal static class WorldObjectWriters
{
    /// <summary>
    /// Spawn Entity (clientbound 0x01): создаёт объект мира на клиенте. Velocity = 0 (объект
    /// в покое), Data = 0 (нет объектных данных). Head Yaw = body Yaw — отдельный серверный
    /// head-look ляжет в подшаге поворота.
    /// </summary>
    public static void WriteSpawnEntity(ref PacketStreamWriter writer, ProtoEntity entity,
        WorldObjectKind kind, Guid uuid, in TransformInf transform)
    {
        writer.WriteVarInt(0x01)                       // packet id — Spawn Entity
            .WriteVarInt((int)entity + 1)              // Entity ID — VarInt (НЕ Int)
            .WriteUuid(uuid)                           // Entity UUID
            .WriteVarInt((int)kind)                    // Type = minecraft:entity_type registry-ID
            .WriteDouble(transform.Position.X)         // X
            .WriteDouble(transform.Position.Y)         // Y
            .WriteDouble(transform.Position.Z)         // Z
            .WriteLpVec3(0, 0, 0)                      // Velocity — LpVec3, игрок в покое (1 байт: 0x00)
            .WriteAngle(transform.Pitch)               // Pitch (Angle)
            .WriteAngle(transform.Yaw)                 // Yaw (Angle)
            .WriteAngle(transform.Yaw)                 // Head Yaw (Angle) — пока = body yaw
            .WriteVarInt(0);                           // Data — последнее поле, нет объектных данных
    }

    /// <summary>
    /// move_entity_pos (clientbound 0x35): малое перемещение сущности. Дельта — fixed-point 1/4096
    /// (Short), абсолютный лимит ±8 блоков/пакет. On Ground — TODO: пока всегда false (прокинуть из
    /// ввода в отдельном подшаге, влияет на анимации ходьбы). Без rotation — поворот отдельным пакетом.
    /// </summary>
    public static void WriteMoveEntityPos(ref PacketStreamWriter writer, ProtoEntity entity,
        short deltaX, short deltaY, short deltaZ, bool onGround)
    {
        writer.WriteVarInt(0x35)                       // packet id — Update Entity Position
            .WriteVarInt((int)entity + 1)              // Entity ID (VarInt)
            .WriteShort(deltaX)                        // Delta X (fixed-point 1/4096)
            .WriteShort(deltaY)                        // Delta Y
            .WriteShort(deltaZ)                        // Delta Z
            .WriteBool(onGround);                      // On Ground
    }
    
    /// <summary>
    /// remove_entities (clientbound 0x4D): удаляет сущности на клиенте. VarInt(count) + count × VarInt(entityId).
    /// Entity ID = (int)entity + 1 (тот же вывод, что в Spawn Entity).
    /// </summary>
    public static void WriteRemoveEntities(ref PacketStreamWriter writer, ProtoEntity entity)
    {
        writer.WriteVarInt(0x4D)                        // packet id — Remove Entities
            .WriteVarInt(1)                             // 1 сущность
            .WriteVarInt((int)entity + 1);              // Entity ID
    }
    
    /// <summary>
    /// move_entity_pos_rot (clientbound 0x36): перемещение + поворот. Дельта — fixed-point 1/4096 (Short),
    /// углы — Angle (1 байт). Когда изменились и позиция, и поворот — один пакет вместо двух.
    /// </summary>
    public static void WriteMoveEntityPosRot(ref PacketStreamWriter writer, ProtoEntity entity,
        short deltaX, short deltaY, short deltaZ, float yaw, float pitch, bool onGround)
    {
        writer.WriteVarInt(0x36)                       // packet id — Update Entity Position and Rotation
            .WriteVarInt((int)entity + 1)              // Entity ID (VarInt)
            .WriteShort(deltaX)                        // Delta X (fixed-point 1/4096)
            .WriteShort(deltaY)                        // Delta Y
            .WriteShort(deltaZ)                        // Delta Z
            .WriteAngle(yaw)                           // Yaw (Angle)
            .WriteAngle(pitch)                         // Pitch (Angle)
            .WriteBool(onGround);                      // On Ground
    }

    /// <summary>
    /// rotate_head / Set Head Rotation (clientbound 0x53): поворот головы сущности. Для игрока
    /// голова крутится независимо от тела — body-yaw реплицируется через move_entity_rot, head-yaw
    /// — через этот пакет. Без него тело и голова рассинхронизируются («смотрят врозь»).
    /// </summary>
    public static void WriteRotateHead(ref PacketStreamWriter writer, ProtoEntity entity, float headYaw)
    {
        writer.WriteVarInt(0x53)                       // packet id — Set Head Rotation
            .WriteVarInt((int)entity + 1)              // Entity ID (VarInt)
            .WriteAngle(headYaw);                      // Head Yaw (Angle)
    }
    
    /// <summary>
    /// move_entity_rot (clientbound 0x38): только поворот, без перемещения. Игрок крутится на месте.
    /// </summary>
    public static void WriteMoveEntityRot(ref PacketStreamWriter writer, ProtoEntity entity,
        float yaw, float pitch, bool onGround)
    {
        writer.WriteVarInt(0x38)                       // packet id — Update Entity Rotation
            .WriteVarInt((int)entity + 1)              // Entity ID (VarInt)
            .WriteAngle(yaw)                           // Yaw (Angle)
            .WriteAngle(pitch)                         // Pitch (Angle)
            .WriteBool(onGround);                      // On Ground
    }

    /// <summary>
    /// player_info_remove (clientbound 0x45): убирает игрока из TAB-листа. VarInt(count) + count × UUID.
    /// Шлётся ПОСЛЕ remove_entities (симметрично спавну, где info_add шёл до spawn).
    /// </summary>
    public static void WriteUserInfoRemove(ref PacketStreamWriter writer, Guid uuid)
    {
        writer.WriteVarInt(0x45)                        // packet id — Player Info Remove
            .WriteVarInt(1)                             // 1 игрок
            .WriteUuid(uuid);                           // UUID
    }
    
    /// <summary>
    /// player_info_update (clientbound 0x46), действие Add (маска 0x1D = Add+Gamemode+Listed+Latency):
    /// добавляет игрока в TAB-лист. <b>Обязательно до Spawn Entity</b> — иначе клиент игнорирует
    /// сущность игрока. Тот же формат, что использует JoinTabListBundle, вынесен сюда как
    /// единое место записи (один факт — одно место).
    /// </summary>
    public static void WriteUserInfoAdd(ref PacketStreamWriter writer, Guid uuid, string username)
    {
        writer.WriteVarInt(0x46)                       // packet id — player_info_update
            .WriteByte(0x1D)                           // Actions: Add+Gamemode+Listed+Latency
            .WriteVarInt(1)                            // 1 игрок
            .WriteUuid(uuid);                          // UUID

        // --- 0x01: Add Player ---
        writer.WriteString(username);                  // Name (String16)
        writer.WriteVarInt(0);                         // Properties length — без скинов (офлайн)

        // --- 0x02: Initialize Chat — пропущено (бит не установлен) ---

        // --- 0x04: Update Game Mode ---
        writer.WriteVarInt(WorldConstants.GAME_MODE);  // Game Mode

        // --- 0x08: Update Listed ---
        writer.WriteBool(true);                        // Listed

        // --- 0x10: Update Latency ---
        writer.WriteVarInt(0);                         // Ping — 0 (нет измерения)
    }
}