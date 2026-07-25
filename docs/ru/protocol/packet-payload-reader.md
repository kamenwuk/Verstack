# PacketPayloadReader — чтение полей пакета
> `src/Verstack.Protocol/PacketPayloadReader.cs`

Последовательно читает типизированные поля из готового тела пакета (полученного через `PacketFrameReader.Current`).
Любой метод возвращает `false`, если данные повреждены.

## Типичное использование
---

```csharp
var frameReader = new PacketFrameReader(buffer);
while (frameReader.MoveNext())
{
    var reader = new PacketPayloadReader(frameReader.Current);

    if (!reader.TryReadVarInt(out int packetId))
        return; // повреждённый пакет

    // чтение полей конкретного пакета ...
}
```

## API
---

### Конструктор
    PacketPayloadReader(ReadOnlySequence<byte> payload) — создаёт читатель для тела одного пакета.

### Свойства
    long ConsumedBytes — сколько байт уже прочитано из полезной нагрузки.

### Методы

| Тип поля Minecraft | Метод |
|---|---|
| VarInt (версия протокола, id) | `bool TryReadVarInt(out int value)` |
| Беззнаковое short (порт) | `bool TryReadUShortBigEndian(out ushort value)` |
| Знаковое long (timestamp) | `bool TryReadInt64BigEndian(out long value)` |
| Строка в UTF‑8 с префиксом длины | `bool TryReadString(out string? value)` |
| UUID (16 байт big‑endian) | `bool TryReadUuid(out Uuid value)` |

Все методы при успехе сдвигают внутренний курсор.
TryReadString выделяет управляемую строку (редко — пара полей на handshake/login).
TryReadUuid возвращает собственный тип Uuid, сохраняющий wire‑порядок байт, в отличие от System.Guid.

→ [Слой Protocol](index.md)

