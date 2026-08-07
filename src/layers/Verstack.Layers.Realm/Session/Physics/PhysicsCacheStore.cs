using Verstack.Layers.Realm.Movement;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Session.Physics;

/// <summary>
/// Пулы модуля физики/пространственного состояния: авторитетное положение сущности в мире
/// (<see cref="TransformInf"/>) и transient-запрос ввода за тик (<see cref="MoveReq"/>).
/// Оба компонента могут принадлежать как игроку, так и любой сущности (AI, knockback).
/// Сюда же лягут velocity/gravity/collision по мере реализации.
/// </summary>
internal sealed class PhysicsCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<TransformInf> Transforms = null!;
    internal readonly ProtoPool<MoveReq> MoveReqs = null!;
}