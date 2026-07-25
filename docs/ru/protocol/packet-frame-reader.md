# PacketFrameReader — чтение фреймов
> `src/Verstack.Protocol/PacketFrameReader.cs`

Читает кадры с VarInt-префиксом длины из последовательности байт.  
Решает задачу фрейминга поверх TCP-потока: разбивает сплошной поток байт на отдельные пакеты Minecraft.

Поддерживает итерацию через `foreach`.

## Зачем нужен фрейминг
---

TCP передаёт поток байт без границ сообщений.  
Один `ReadAsync` может вернуть несколько пакетов, обрывок пакета или часть заголовка.

Правило «VarInt-длина + тело» указывает, где заканчивается один пакет и начинается следующий:

```
[ VarInt: длина payload ][ payload: N байт ]
```

`PacketFrameReader` читает VarInt, проверяет наличие полного payload и отдаёт его как `ReadOnlySequence<byte>`.

Подробнее о кодировании длины — [VarInt](varint.md).

## Состояния
---

| Статус | Значение |
|--------|----------|
| `Complete` | Кадр прочитан (`MoveNext()` вернул `true`). |
| `Partial`  | Недостаточно данных — дождаться следующего `ReadAsync`. |
| `Malformed`| Бит продолжения в 5-м байте или длина больше лимита. Разорвать соединение. |

## API
---

```csharp
public ref struct PacketFrameReader
{
    public PacketFrameReader(ReadOnlySequence<byte> input, int maxPacketSize = PacketFrameWriter.DefaultMaxPacketSize);

    public bool MoveNext();
    public ReadOnlySequence<byte> Current { get; }
    public VarInt.ReadStatus Status { get; }
    public SequencePosition ConsumedPosition { get; }
    public PacketFrameReader GetEnumerator();
}
```

> Current — доступен только после успешного MoveNext().

> Status — причина, когда MoveNext() вернул false.

> ConsumedPosition — позиция для PipeReader.AdvanceTo. При Partial указывает на начало неполного кадра, чтобы его байты остались в буфере.

> GetEnumerator() — позволяет foreach (var frame in reader).

→ [Слой Protocol](index.md)