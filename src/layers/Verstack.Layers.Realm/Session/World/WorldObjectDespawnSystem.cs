using Verstack.Engine.Network.Compression;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Layers.Realm.Shared;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Engine.Lifecycle;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Деспавнит уходящие объекты мира: для каждого, кто был заспавнен (<see cref="ItLeaving"/> —
/// дисконнектнулся, но имеет <see cref="SpawnedTag"/>), рассылает оставшимся (<see cref="ItSpawned"/>)
/// remove_entities (0x4D) + player_info_remove (0x45).
///
/// <para><b>Порядок выполнения критичен.</b> <see cref="BridgeCleanupSystem"/> удаляет уходящую
/// сущность (DelEntity + чистка канала) на шаге 2 тика, до игровых систем. Эта система
/// регистрируется с <b>отрицательным весом</b>, чтобы бежать ДО cleanup — иначе она никогда не
/// увидит уходящую сущность. На канал уходящего не пишем (сокет мёртв) — отправляем оставшимся.</para>
///
/// <para>Тип-специфичные дополнения (для игрока — player_info_remove) диспетчеризуются по
/// <see cref="WorldObjectKind"/>, как в <see cref="WorldObjectSpawnSystem"/>. Списки собираются в
/// переиспользуемые буферы (GC-free после первого срабатывания).</para>
/// </summary>
internal sealed class WorldObjectDespawnSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly WorldObjectCacheStore _worldObjects = null!;
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    // Переиспользуемые снимки — GC-free после первого срабатывания. Деспавн — редкое событие.
    private readonly List<ProtoEntity> _leaving = new();
    private readonly List<ProtoEntity> _remaining = new();

    public void Init(IProtoSystems systems) { }

    public void Run()
    {
        _leaving.Clear();
        foreach (var entity in _worldObjects.ItLeaving)
            _leaving.Add(entity);

        if (_leaving.Count == 0)
            return;

        _remaining.Clear();
        foreach (var entity in _worldObjects.ItSpawned)
            _remaining.Add(entity);

        // Нет получателей (ушли все) — рассылать некому. Уходящие очистит BridgeCleanupSystem.
        if (_remaining.Count == 0)
            return;

        foreach (var leaver in _leaving)
        {
            ref readonly var leaverObject = ref _worldObjects.WorldObjects.Get(leaver);

            foreach (var observer in _remaining)
            {
                var channel = _bridgeStateCacheStore.GetChannel(observer);
                if (channel == null)
                    continue;

                using var lease = OutboundLease.Acquire(channel, _compressor);

                // remove_entities ПЕРВЫМ — сущность исчезает из мира.
                var wRemove = lease.Begin();
                WorldObjectWriters.WriteRemoveEntities(ref wRemove, leaver);
                lease.Commit(ref wRemove);

                // post-remove-extras: для игрока — player_info_remove ВТОРЫМ (чистка tab-list).
                var wExtra = lease.Begin();
                WritePostRemoveExtras(ref wExtra, in leaverObject);
                lease.Commit(ref wExtra);
            }
        }
    }

    /// <summary>
    /// Тип-специфичные пакеты, отправляемые ПОСЛЕ remove_entities. Диспетч по
    /// <see cref="WorldObjectKind"/> — зеркально к <see cref="WorldObjectSpawnSystem.WritePreSpawnExtras"/>.
    /// </summary>
    private void WritePostRemoveExtras(ref PacketStreamWriter writer, in WorldObjectInf worldObject)
    {
        switch (worldObject.Kind)
        {
            case WorldObjectKind.User:
                // Игрока — убрать из TAB-листа.
                WorldObjectWriters.WriteUserInfoRemove(ref writer, worldObject.Uuid);
                break;

            // case WorldObjectKind.Zombie: // будущий подшаг: мобу — ничего дополнительно
            //     break;
        }
    }
}