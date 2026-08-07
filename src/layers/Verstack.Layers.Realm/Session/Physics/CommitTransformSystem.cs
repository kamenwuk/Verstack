using Verstack.Layers.Realm.Shared;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Session.Physics;

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
/// <para>Работает по <c>ItPlaying</c> — игрокам, прошедшим Join. Регистрируется в
/// <c>RealmLayer.Init</c> ПОСЛЕ <c>InboundDispatcherSystem</c> (MoveReq пишется бандлами
/// входящих пакетов) и ДО <c>ChunkObserverSystem</c> (читает зафиксированный transform).</para>
/// </summary>
internal sealed class CommitTransformSystem : IProtoRunSystem
{
    [DI] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    [DI] private readonly PhysicsCacheStore _physics = null!;

    public void Run()
    {
        foreach (var entity in _userSessionCacheStore.ItPlaying)
        {
            // Нет MoveReq — позиция не менялась, transform остаётся прежним.
            if (!_physics.MoveReqs.Has(entity))
                continue;

            ref var move = ref _physics.MoveReqs.Get(entity);
            ref var transform = ref _physics.Transforms.Get(entity);

            // Фиксация: ввод клиента становится авторитетной позицией сервера.
            transform.Position = move.Position;

            // Запрос обработан — снимаем (живёт один тик).
            _physics.MoveReqs.Del(entity);
        }
    }
}