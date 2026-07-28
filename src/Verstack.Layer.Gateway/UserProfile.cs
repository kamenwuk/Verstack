namespace Verstack.Layer.Gateway;

/// <summary>
/// Профиль игрока на сущности подключения. Заполняется в Login-фазе из Login Start
/// (UUID в offline-режиме генерируется детерминированно) и хранится для следующих фаз
/// (Configuration/Play). <see cref="NetworkSession"/> хранит транспортные данные канала,
/// этот компонент — игровые.
/// </summary>
internal readonly struct UserProfile(Guid uuid, string username)
{
    public readonly Guid Uuid = uuid;
    public readonly string Username = username;
}