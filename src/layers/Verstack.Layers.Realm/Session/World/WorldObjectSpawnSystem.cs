using Verstack.Engine.Network.Compression;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Layers.Global.User;
using Verstack.Layers.Realm.Shared;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Engine.Lifecycle;
using Verstack.Layers.Realm.Session.Physics;
using Verstack.Shared.Debug;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Спавнит объекты мира друг для друга: ловит свежие (<see cref="ItUnspawned"/> — получили
/// <see cref="WorldObjectInf"/>, но без <see cref="SpawnedTag"/>) и для каждого:
/// - рассылает каждому спавнутому pre-spawn-extras (игроку — player_info_update add) +
///   Spawn Entity (0x01) о новичке;
/// - отправляет новичку те же пакеты по каждому спавнутому.
/// После рассылки сеёт <see cref="SpawnedTag"/> — повторных спавнов не будет.
///
/// <para>Нейтральна к типу объекта: общий Spawn Entity одинаковый, тип-специфичные дополнения
/// диспетчеризуются по <see cref="WorldObjectKind"/> (сейчас только Player). Добавление мобов —
/// новая ветка в <see cref="WritePreSpawnExtras"/> без правок общего пути.</para>
///
/// <para>Без spatial-фильтра: каждый видит каждого (O(n²), норм для малого онлайна; proximity —
/// отдельная система поверх chunk-viewport в будущем). Списки собираются в переиспользуемые
/// буферы (GC-free после первого срабатывания); модификация фильтра (добавление тега) идёт вне
/// итерации фильтра — безопасно для EcsProto. Регистрируется ПОСЛЕ <see cref="CommitTransformSystem"/>
/// — к моменту спавна позиция уже зафиксирована.</para>
/// </summary>
internal sealed class WorldObjectSpawnSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    [DI] private readonly WorldObjectCacheStore _worldObjects = null!;
    [DI] private readonly PhysicsCacheStore _physics = null!;
    
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    // Переиспользуемые снимки — GC-free после первого срабатывания. Спавн — редкое событие.
    private readonly List<ProtoEntity> _spawned = new();
    private readonly List<ProtoEntity> _fresh = new();

    public void Init(IProtoSystems systems) { }

    public void Run()
    {
        _spawned.Clear();
        foreach (var entity in _worldObjects.ItSpawned)
            _spawned.Add(entity);

        _fresh.Clear();
        foreach (var entity in _worldObjects.ItUnspawned)
            _fresh.Add(entity);

        if (_fresh.Count == 0)
            return;

        foreach (var fresh in _fresh)
        {
            var freshChannel = _bridgeStateCacheStore.GetChannel(fresh);
            if (freshChannel == null)
                continue;

            ref readonly var freshObject = ref _worldObjects.WorldObjects.Get(fresh);
            ref readonly var freshTransform = ref _physics.Transforms.Get(fresh);

            using var freshLease = OutboundLease.Acquire(freshChannel, _compressor);

            // Спавним fresh каждому уже-обработанному в этом тике (включая тех, кого тегировали
            // предыдущими итерациями этого же цикла — _spawned растёт по ходу обработки).
            foreach (var spawned in _spawned)
            {
                var spawnedChannel = _bridgeStateCacheStore.GetChannel(spawned);
                if (spawnedChannel == null)
                    continue;

                ref readonly var spawnedObject = ref _worldObjects.WorldObjects.Get(spawned);
                ref readonly var spawnedTransform = ref _physics.Transforms.Get(spawned);

                // 1) спавнутый видит свежего — на канал спавнутого. Каждый пакет = свой Begin/Commit.
                using var spawnedLease = OutboundLease.Acquire(spawnedChannel, _compressor);

                var wInfo1 = spawnedLease.Begin();
                WritePreSpawnExtras(ref wInfo1, fresh, in freshObject);
                spawnedLease.Commit(ref wInfo1);

                var wSpawn1 = spawnedLease.Begin();
                WorldObjectWriters.WriteSpawnEntity(ref wSpawn1, fresh, freshObject.Kind, freshObject.Uuid, in freshTransform);
                spawnedLease.Commit(ref wSpawn1);

                // 2) свежий видит спавнутого — на канал свежего (общий lease этого тика). Тоже два пакета.
                var wInfo2 = freshLease.Begin();
                WritePreSpawnExtras(ref wInfo2, spawned, in spawnedObject);
                freshLease.Commit(ref wInfo2);

                var wSpawn2 = freshLease.Begin();
                WorldObjectWriters.WriteSpawnEntity(ref wSpawn2, spawned, spawnedObject.Kind, spawnedObject.Uuid, in spawnedTransform);
                freshLease.Commit(ref wSpawn2);
            }

            // Точка отсчёта для будущих move-дельт: после спавна позиция = TransformInf.
            // Без этого WorldObjectMovementSystem будет считать дельту от default(Vector3).
            _worldObjects.Synced.Add(fresh) = new SyncedInf
            {
                Position = freshTransform.Position,
                Yaw = freshTransform.Yaw,
                Pitch = freshTransform.Pitch,
                OnGround = freshTransform.OnGround
            };
            _worldObjects.SpawnedTags.Add(fresh);
            // Рассылка по этому новичку завершена — тегируем, в ItUnspawned больше не попадёт.
            _spawned.Add(fresh);
        }
    }

    /// <summary>
    /// Тип-специфичные пакеты, которые надо отправить ДО Spawn Entity (чтобы клиент был готов
    /// принять сущность). Диспетч по <see cref="WorldObjectKind"/> — единственная точка, где
    /// система знает про виды. Расширение новыми типами — добавление ветки.
    /// </summary>
    private void WritePreSpawnExtras(ref PacketStreamWriter writer, ProtoEntity entity, in WorldObjectInf worldObject)
    {
        switch (worldObject.Kind)
        {
            case WorldObjectKind.User:
                // Игроку требуется запись в TAB-листе до спавна, иначе клиент игнорирует сущность.
                ref readonly var profile = ref _userSessionCacheStore.UserProfiles.Get(entity);
                WorldObjectWriters.WriteUserInfoAdd(ref writer, worldObject.Uuid, profile.Username);
                break;

            // case WorldObjectKind.Zombie: // будущий подшаг: мобу — set_entity_data (метадата)
            //     break;
        }
    }
}