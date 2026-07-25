# Uuid — 128-bit Identifier
> `src/Verstack.Protocol/Uuid.cs`

Minecraft uses 128-bit UUIDs for players, entities, and other objects.  
On the wire, a UUID is sent as 16 big-endian bytes without dashes.

A dedicated `Uuid` type avoids `System.Guid`'s mixed-endian in‑memory layout,
which would silently break byte‑for‑byte comparisons with the protocol stream.

## Wire format

16 bytes, big-endian, no dashes.

```
		128 bits (16 bytes)
 ┌────────────────────────────┐
 │ byte 0 (MSB) byte 15 (LSB) │
 └────────────────────────────┘
 Example value: `550e8400-e29b-41d4-a716-446655440000` (dashed form)  
 Wire bytes: `55 0E 84 00 E2 9B 41 D4 A7 16 44 66 55 44 00 00`
```

## API surface

### Struct `Uuid`

- **`static Uuid Read(ReadOnlySpan<byte> bytes)`** — reads a UUID from exactly 16 big-endian bytes.
- **`void Write(Span<byte> bytes)`** — writes the UUID as 16 big-endian bytes.
- **`bool Equals(Uuid other)`** — byte‑for‑byte equality.
- **`override string ToString()`** — canonical dashed form (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`), lowercase.

All methods throw `ArgumentException` if the span is shorter than 16 bytes.

### Reading a UUID from a packet

`PacketPayloadReader.TryReadUuid` reads 16 bytes from the payload and calls `Uuid.Read`:

```csharp
PacketPayloadReader reader = ...;
if (reader.TryReadUuid(out Uuid uuid))
    Console.WriteLine(uuid); // 550e8400-e29b-41d4-a716-446655440000
```

→ [Protocol layer](index.md)