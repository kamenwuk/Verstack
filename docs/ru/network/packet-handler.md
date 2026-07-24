# Packet handler

Packet handler — шов между Network и вышележащим слоем. Network определяет контракт; Minecraft (или любой другой прикладной слой) даёт реализацию; App их связывает.

```
public interface IPacketHandler
{
    void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}
```

`payload` — тело целого кадра, уже очищенное от префикса длины scanner'ом. `output` — сторона записи соединения; handler пишет сюда обрамлённые ответы через `PacketFraming`. Метод синхронный — handler только буферизует; flush — задача `SessionLifetime`.

Этот интерфейс — вся поверхность контакта между Network и Minecraft. Добавление нового состояния протокола или типа пакета никогда не затрагивает Network — только новую реализацию `IPacketHandler` и связку в `App`.

→ [Жизнь соединения](server-lifetime.md)
