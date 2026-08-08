using Verstack.Layers.Realm.Shared;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Engine.Bridge;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Пулы и фильтры домена объектов мира. Владеет <see cref="WorldObjectInf"/> (идентичность),
/// <see cref="SpawnedTag"/> (состояние спавна) и строит два итератора поверх
/// <see cref="UserSessionCacheStore.ItPlaying"/>: <see cref="ItUnspawned"/> — свежие объекты,
/// кому нужен спавн; <see cref="ItSpawned"/> — уже спавнутые, источник для рассылки.
/// </summary>
internal sealed class WorldObjectCacheStore : ProtoAspectInject
{
    internal ProtoItExc ItUnspawned { get; private set; } = null!;
    internal ProtoIt ItSpawned { get; private set; } = null!;
    internal ProtoItExc ItLeaving { get; private set; } = null!;
    
    internal readonly ProtoPool<WorldObjectInf> WorldObjects = null!;
    internal readonly ProtoPool<SpawnedTag> SpawnedTags = null!;
    internal readonly ProtoPool<SyncedInf> Synced = null!;
    
    public override void Init(ProtoWorld world)
    {
        // ItPlaying уже несёт Connection+Session+Profile и исключает FlowState — берём как базу.
        var playing = world.Aspect<UserSessionCacheStore>().ItPlaying;

        // Свежие: тот же набор + WorldObjectInf, но БЕЗ SpawnedTag.
        var unspawnedInc = new List<Type>(playing.IncTypes) { typeof(WorldObjectInf) };
        var unspawnedExc = new List<Type>(playing.ExcTypes) { typeof(SpawnedTag) };
        ItUnspawned = new ProtoItExc(unspawnedInc.ToArray(), unspawnedExc.ToArray());

        // Спавнутые: ItPlaying + SpawnedTag. Include-only → ProtoIt (без exclude).
        var spawnedInc = new List<Type>(playing.IncTypes)
        {
            typeof(WorldObjectInf),
            typeof(SpawnedTag),
            //   typeof(SyncedInf)   // ← добавить: репликация движения работает только когда Synced уже сеялся
        };
        ItSpawned = new ProtoIt(spawnedInc.ToArray());

        var leaving = world.Aspect<BridgeStateCacheStore>().DisconnectedFilter;
        var leavingInc = new List<Type>(leaving.IncTypes) { typeof(WorldObjectInf), typeof(SpawnedTag) };
        var leavingExc = new List<Type>(leaving.ExcTypes);
        ItLeaving = new ProtoItExc(leavingInc.ToArray(), leavingExc.ToArray());
        
        base.Init(world);
    }
}