using Verstack.Engine.Network.Compression;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Layers.Realm.Shared;
using Verstack.Layers.Realm.Session.Physics;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Engine.Lifecycle;
using Verstack.Shared.Maths;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Реплицирует движение спавнутых объектов: сравнивает авторитетный <see cref="TransformInf"/>
/// с последним синхронизированным <see cref="SyncedInf"/> и при diff шлёт <c>move_entity_pos</c>
/// (0x35) остальным спавнутым. После отправки обновляет <see cref="SyncedInf"/>.
///
/// <para>Только позиция. Поворот реплицируется отдельно (<c>move_entity_rot</c> 0x38) — следующий
/// подшаг. Без spatial-фильтра: каждая дельта уходит каждому спавнутому (O(n²)). Регистрируется
/// ПОСЛЕ <see cref="CommitTransformSystem"/> — позиция уже зафиксирована из ввода.</para>
/// </summary>
internal sealed class WorldObjectMovementSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly PhysicsCacheStore _physics = null!;
    [DI] private readonly WorldObjectCacheStore _worldObjects = null!;
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    public void Init(IProtoSystems systems) { }

    public void Run()
    {
        foreach (var entity in _worldObjects.ItSpawned)
        {
            ref readonly var transform = ref _physics.Transforms.Get(entity);
            ref var synced = ref _worldObjects.Synced.Get(entity);

            // --- diff позиции (fixed-point 1/4096) ---
            int dx = (int)Math.Round((transform.Position.X - synced.Position.X) * 4096.0);
            int dy = (int)Math.Round((transform.Position.Y - synced.Position.Y) * 4096.0);
            int dz = (int)Math.Round((transform.Position.Z - synced.Position.Z) * 4096.0);
            bool posChanged = dx != 0 || dy != 0 || dz != 0;

            // --- diff поворота (Angle — шаг 1/256 оборота, так что сравниваем с допуском полушага) ---
            bool rotChanged = !AngleEquals(transform.Yaw, synced.Yaw) || !AngleEquals(transform.Pitch, synced.Pitch);

            // Ничего не поменялось — пакетов нет.
            if (!posChanged && !rotChanged && transform.OnGround == synced.OnGround)
                continue;

            // Выбор пакета по тому, что изменилось.
            PacketKind kind;
            if (posChanged && rotChanged) kind = PacketKind.PosRot;
            else if (posChanged)          kind = PacketKind.Pos;
            else                          kind = PacketKind.Rot;

            // Clamp delta для Short-диапазона (±8 блоков). При TP за пределы — потеря точности,
            // но серверных TP пока нет; для ходьбы диапазона хватает.
            short sdx = (short)Math.Clamp(dx, short.MinValue, short.MaxValue);
            short sdy = (short)Math.Clamp(dy, short.MinValue, short.MaxValue);
            short sdz = (short)Math.Clamp(dz, short.MinValue, short.MaxValue);

            // Рассылаем каждому другому спавнутому.
            foreach (var observer in _worldObjects.ItSpawned)
            {
                if (observer == entity)
                    continue;

                var channel = _bridgeStateCacheStore.GetChannel(observer);
                if (channel == null)
                    continue;

                using var lease = OutboundLease.Acquire(channel, _compressor);
                var w = lease.Begin();
                switch (kind)
                {
                    case PacketKind.PosRot:
                        WorldObjectWriters.WriteMoveEntityPosRot(ref w, entity, sdx, sdy, sdz, transform.Yaw, transform.Pitch, transform.OnGround);
                        break;
                    case PacketKind.Pos:
                        WorldObjectWriters.WriteMoveEntityPos(ref w, entity, sdx, sdy, sdz, transform.OnGround);
                        break;
                    case PacketKind.Rot:
                        WorldObjectWriters.WriteMoveEntityRot(ref w, entity, transform.Yaw, transform.Pitch, transform.OnGround);
                        break;
                }
                lease.Commit(ref w);
                
                // При изменении поворота — синхронизируем и head-yaw (body и head для игрока совпадают,
// но реплицируются разными пакетами). Без rotate_head голова осталась бы на спавн-япе.
                if (rotChanged)
                {
                    var wHead = lease.Begin();
                    WorldObjectWriters.WriteRotateHead(ref wHead, entity, transform.Yaw);
                    lease.Commit(ref wHead);
                }
            }

            // Обновляем точку отсчёта фактическим отправленным (с clamp'ом — чтобы накопленная
            // ошибка при TP не росла).
            synced.Position = new Vector3(
                synced.Position.X + sdx / 4096.0f,
                synced.Position.Y + sdy / 4096.0f,
                synced.Position.Z + sdz / 4096.0f);
            synced.Yaw = transform.Yaw;
            synced.Pitch = transform.Pitch;
            synced.OnGround = transform.OnGround;
        }
    }

    /// <summary>
    /// Сравнение углов с допуском в полшага Angle (1/256 оборота / 2 ≈ 0.7°). Меньше — клиент
    /// не отличит, пакет избыточен.
    /// </summary>
    private static bool AngleEquals(float a, float b)
    {
        const float HALF_STEP = (360f / 256f) * 0.5f;
        float d = Math.Abs(a - b) % 360f;
        if (d > 180f) d = 360f - d;
        return d < HALF_STEP;
    }

    private enum PacketKind { Pos, Rot, PosRot }
}