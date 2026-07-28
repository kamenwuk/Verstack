namespace Verstack.Layer.Gateway;

/// <summary>
/// Профиль игрока на сущности подключения. Заполняется поэтапно: <see cref="Uuid"/>/<see cref="Username"/>
/// — в Login-фазе из Login Start (UUID в offline-режиме генерируется детерминированно),
/// <see cref="Locale"/> — в Configuration из Client Information. Хранится для следующих фаз (Play).
/// <see cref="NetworkSession"/> хранит транспортные данные канала, этот компонент — игровые.
/// </summary>
internal readonly struct UserProfile(Guid uuid, string username, string locale = null)
{
    public readonly Guid Uuid = uuid;
    public readonly string Username = username;
    public readonly string Locale = locale;
}