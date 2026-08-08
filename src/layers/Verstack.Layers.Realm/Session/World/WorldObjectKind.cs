namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Тип объекта мира. Значение одновременно = registry-ID <c>minecraft:entity_type</c>
/// (ванильный protocol_id) и ключ диспетча тип-специфичных дополнений при спавне/деспавне.
/// Поэтому пишется напрямую в поле Type пакета Spawn Entity как <c>(int)</c>, а система
/// репликации по значению определяет, какие дополнительные пакеты нужны (игроку —
/// player_info_update до спавна; мобу в будущем — set_entity_data после).
///
/// <para>Реестр entity_type клиенту не отправляется (отсутствует в
/// <c>SyncedRegistryCatalog</c>), поэтому клиент использует встроенный ванильный реестр и
/// ID должны совпадать с ванильными. Расширение новыми типами — добавление значения.</para>
/// </summary>
internal enum WorldObjectKind : int
{
    /// <summary>
    /// minecraft:player, protocol_id 156. Спавн: <c>player_info_update</c> (add) ДО Spawn Entity.
    /// Деспавн: <c>player_info_remove</c> ПОСЛЕ <c>remove_entities</c>.
    /// </summary>
    User = 156,
}