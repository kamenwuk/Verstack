# Uuid — 128-битный идентификатор
> `src/Verstack.Protocol/Uuid.cs`

Minecraft использует 128-битные UUID для игроков, сущностей и других объектов.
По сети UUID передаётся как 16 байт в big-endian, без дефисов.

Собственный тип `Uuid` позволяет избежать проблем, связанных с тем,
что `System.Guid` хранит байты в смешанном порядке (mixed-endian),
что нарушило бы побайтовое сравнение с потоком протокола.

## Формат на проводе

16 байт, big-endian, без дефисов.

```
		128 bits (16 bytes)
 ┌────────────────────────────┐
 │ byte 0 (MSB) byte 15 (LSB) │
 └────────────────────────────┘
 Пример значения: `550e8400-e29b-41d4-a716-446655440000` (каноническая форма)  
 Байты на проводе: `55 0E 84 00 E2 9B 41 D4 A7 16 44 66 55 44 00 00`
```

## API

### Структура `Uuid`

- **`static Uuid Read(ReadOnlySpan<byte> bytes)`** — читает UUID ровно из 16 big-endian байт.
- **`void Write(Span<byte> bytes)`** — записывает UUID как 16 big-endian байт.
- **`bool Equals(Uuid other)`** — побайтовое сравнение.
- **`override string ToString()`** — каноническая форма с дефисами (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`), строчные буквы.

Все методы выбрасывают `ArgumentException`, если переданный span короче 16 байт.

### Чтение UUID из пакета

Метод `PacketPayloadReader.TryReadUuid` читает 16 байт из тела пакета и вызывает `Uuid.Read`:

```csharp
PacketPayloadReader reader = ...;
if (reader.TryReadUuid(out Uuid uuid))
    Console.WriteLine(uuid); // 550e8400-e29b-41d4-a716-446655440000
```

→ [Protocol layer](index.md)