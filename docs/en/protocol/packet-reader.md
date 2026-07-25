# Reading packet fields

`PacketReader` reads the fields of one packet from a complete frame's payload — what `PacketFrameScanner` yields after stripping the length prefix. This is the abstraction at which per-packet parsers in the Minecraft layer assemble DTOs out of bytes.

```csharp
var reader = new PacketReader(payload);   // payload: ReadOnlySequence<byte> of one frame
reader.TryReadVarInt(out int packetId);   // then the fields of the packet body
```

## Why a separate type

`PacketFrameScanner` solves the outer-level task: split the TCP byte stream into frames. At that level it is critical to distinguish *Partial* (wait for more bytes) from *Malformed* (drop the connection) — that is backpressure.

`PacketReader` solves the inner-level task: parse the fields of an already-complete frame. There is no "wait for more bytes" here — the frame arrived whole, and if bytes run out mid-field, that is not partial but malformed from the client. So the return type is `bool` (valid / not), not `VarInt.ReadStatus`. One outcome is enough: the dispatcher treats `false` as a garbage packet.

This asymmetry with the scanner is deliberate: they play different roles, and the return type reflects the role.

## Why `ref struct`

For the same reasons as `PacketFrameScanner`: stack-only, zero allocations, no boxing. Internally `PacketReader` holds a `SequenceReader<byte>` (a cursor over a `ReadOnlySequence`), which is itself a `ref struct`; the outer type repeats the constraint.

The "cannot cross an `await`" constraint does not bite here: `PacketReader` lives only in the handler's synchronous section — between reading a frame and `FlushAsync`. After the flush it is no longer needed.

## Primitives

| Minecraft field | Wire format | Method |
|---|---|---|
| Integer (protocol version, id) | VarInt (LEB128) | `TryReadVarInt(out int)` |
| Port (Handshake) | 2 bytes big-endian | `TryReadUShortBigEndian(out ushort)` |
| Timestamp (Ping/Pong) | 8 bytes big-endian | `TryReadInt64BigEndian(out long)` |
| UUID (player in Login/Play) | 16 bytes big-endian, no dashes | `TryReadUuid(out Uuid)` |
| String (address, player name) | `[VarInt(len)][UTF-8]` | `TryReadString(out string?)` |

Big-endian is the network byte order, and Minecraft follows it for fixed-width fields. The `BigEndian` suffix in the method name states the wire contract explicitly: the reading code can see at a glance which order is expected. VarInt fields carry no endianness — bytes flow in sequence with continuation bits.

Big-endian reading is built on the signed overloads of `SequenceReader.TryReadBigEndian` (the BCL ships only a signed reader), with an `unchecked` cast for the unsigned case: the bit pattern is preserved, `0xFFFF` reads back as `65535`, not `-1`.

## Strings

`TryReadString` reads the VarInt length, checks it against the remaining frame, and decodes UTF-8. The string allocation is unavoidable for text, but this is not a hot path: a handshake string is 1–2 fields per connection. The hot path (chunks, entities in Play) carries no length-prefixed strings.

Short bodies decode zero-copy through the contiguous-span branch; a segmented payload (rare) degrades to one allocation for a copy, but stays correct.

## UUID

`TryReadUuid` reads 16 big-endian bytes and returns a `Uuid` — a dedicated type in the Protocol layer, not `System.Guid`. The reason: `Guid` stores its first three fields little-endian (mixed-endian), so `new Guid(byte[16])` produces a Guid whose in-memory layout does not match the wire byte-for-byte. Any code that compares or byte-reads a UUID (and Play does this a lot) would fall into the trap. `Uuid` keeps two `ulong`s strictly in wire order — `Equals`/`==` compare byte-equivalent to the wire, `ToString` produces the canonical dashed lowercase form. No allocations on the hot path: a `stackalloc char[36]` plus one allocation for the final string.

A segmented payload (rare) is copied into a `stackalloc byte[16]` before reading.

## Failure

Every method returns `false` on a short read, a negative string length, or a length that exceeds the remaining frame. The causes are not differentiated: a single `false` outcome is enough — how the dispatcher treats it is described at the Minecraft layer.

→ [Protocol layer](index.md)
