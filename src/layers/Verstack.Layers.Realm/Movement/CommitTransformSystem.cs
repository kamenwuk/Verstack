using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Engine.Bridge;
using Verstack.Engine.Lifecycle;
using Verstack.Layers.Realm.User;

namespace Verstack.Layers.Realm.Movement;

/// <summary>
/// Фиксирует позицию игрока в мире: читает ввод <see cref="MoveReq"/> (накопленный за тик
/// от move-пакетов 0x1E/0x1F) и переносит его в авторитетное состояние <see cref="TransformInf"/>.
/// После чтения снимает <see cref="MoveReq"/> — запрос живёт один тик.
///
/// <para>Единственное место записи <see cref="TransformInf"/> для игрока: пока валидации
/// ввода нет (коллизии, анти-чит), позиция из ввода фиксируется как есть. Когда появится
/// серверная коррекция — она будет отдельной системой, пишущей в <see cref="TransformInf"/>
/// (ТП, knockback), а эта система останется каналом ввода.</para>
///
/// <para>Регистрируется в <c>RealmLayer.Init</c> ПОСЛЕ <c>InboundDispatcherSystem</c>
/// (MoveReq пишется бандлами входящих пакетов) и ДО <c>ChunkObserverSystem</c> (читает
/// зафиксированный transform).</para>
/// </summary>
internal sealed class CommitTransformSystem : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly UserSessionCacheStore _realmCache = null!;

    public void Run()
    {
        foreach (var entity in _bridgeStateCacheStore.ConnectedFilter)
        {
            // Нет MoveReq — позиция не менялась, transform остаётся прежним.
            if (!_realmCache.MoveReqs.Has(entity))
                continue;

            ref var move = ref _realmCache.MoveReqs.Get(entity);
            ref var transform = ref _realmCache.Transforms.Get(entity);

            // Фиксация: ввод клиента становится авторитетной позицией сервера.
            transform.Position = move.Position;

            // Запрос обработан — снимаем (живёт один тик).
            _realmCache.MoveReqs.Del(entity);
        }
    }
}