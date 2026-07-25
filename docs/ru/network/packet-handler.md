# Packet handler

Packet handler — шов между Network и вышележащим слоем. Network определяет контракты; Minecraft (или любой другой прикладной слой) даёт реализации; App их связывает.

``` csharp
public interface IPacketHandler
{
    PacketVerdict OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}

public interface IPacketHandlerFactory
{
    IPacketHandler Create();
}
```

`IPacketHandler.OnPacket` — реакция на один кадр. `payload` — тело целого кадра, уже очищенное от префикса длины scanner'ом. `output` — сторона записи соединения; handler пишет сюда обрамлённые ответы через `PacketFraming`. Метод синхронный — handler только буферизует; flush — задача `SessionLifetime`. Возврат `PacketVerdict` — вердикт о судьбе соединения: `Keep` (default) — продолжаем чтение, `Disconnect` — рвём. Вердикт SessionLifetime чтёт **после** flush'а, так что ответ, записанный для этого кадра, уходит до разрыва. Когда handler возвращает `Disconnect` — см. [диспетчер](../minecraft/dispatcher.md); как `SessionLifetime` на это реагирует — [точка решения «рвать»](server-lifetime.md).

`IPacketHandlerFactory.Create` — точка рождения handler'а на каждое соединение. Она существует потому, что у handler'а есть per-connection состояние (текущая фаза протокола), и это состояние не может жить в singleton-объекте, шаримом между всеми соединениями. `TcpServer` вызывает `Create()` в accept-цикле и передаёт свежий handler в `SessionLifetime`. Детали того, что именно хранит handler — на уровне Minecraft; здесь зафиксирован только контракт: «каждое соединение получает свой handler».

Эти два интерфейса — вся поверхность контакта между Network и Minecraft. Добавление нового состояния протокола или типа пакета никогда не затрагивает Network — только новую реализацию `IPacketHandler` (и фабрику, её создающую) плюс связку в `App`.

→ [Жизнь соединения](server-lifetime.md)
