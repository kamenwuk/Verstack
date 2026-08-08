namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Идентичность объекта мира: тип (<see cref="Kind"/>) и сетевой UUID. Характеризует, чем
/// сущность является для репликации: значение <see cref="Kind"/> пишется в поле Type пакета
/// Spawn Entity, <see cref="Uuid"/> — в поле UUID. Нейтрален к тому, игрок это или моб —
/// специфика типа диспетчеризуется в системе по <see cref="Kind"/>.
///
/// <para>Сеётся при появлении объекта в мире (для игрока — в <c>HandoffSeeder</c> при
/// завершении Join, UUID копируется из <c>UserProfile.Uuid</c>). Живёт всю сессию сущности
/// (суффикс <c>Inf</c> по конвенции времени жизни компонентов).</para>
/// </summary>
internal struct WorldObjectInf
{
    /// <summary>Тип объекта. Значение = registry-ID minecraft:entity_type для Spawn Entity.</summary>
    public WorldObjectKind Kind;

    /// <summary>Сетевой UUID объекта — поле UUID пакета Spawn Entity.</summary>
    public Guid Uuid;
}