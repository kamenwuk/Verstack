namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Маркер объекта, уже заспавненного клиентам: система спавна обработала его (расслала
/// остальным / получила остальных). Состояние жизненного цикла самого объекта, а не флаг
/// подсистемы — отличает «свежие» (<see cref="WorldObjectInf"/> без тега) от «спавнутых»
/// (с тегом). Сеётся в <see cref="WorldObjectSpawnSystem"/> после рассылки.
/// </summary>
internal readonly struct SpawnedTag { }