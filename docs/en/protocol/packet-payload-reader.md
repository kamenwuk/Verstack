# PacketPayloadReader — In‑Packet Field Reader
> `src/Verstack.Protocol/PacketPayloadReader.cs`

Reads typed fields sequentially from a complete packet payload (obtained from `PacketFrameReader.Current`).
A `false` return from any method means the payload is malformed.

## Typical usage
---

```csharp
var frameReader = new PacketFrameReader(buffer);
while (frameReader.MoveNext())
{
    var reader = new PacketPayloadReader(frameReader.Current);

    if (!reader.TryReadVarInt(out int packetId))
        return; // malformed packet

    // read protocol‑specific fields ...
}
```

## API
---

### Constructor
	PacketPayloadReader(ReadOnlySequence<byte> payload) — initialises the reader over one packet body.

### Properties
	long ConsumedBytes — bytes already consumed from the payload.

### Methods

| Wire type | Method |
|---|---|
| VarInt (protocol version, IDs) | `bool TryReadVarInt(out int value)` |
| Unsigned short (port) | `bool TryReadUShortBigEndian(out ushort value)` |
| Signed long (timestamp) | `bool TryReadInt64BigEndian(out long value)` |
| Length‑prefixed UTF‑8 string | `bool TryReadString(out string? value)` |
| UUID (16 big‑endian bytes) | `bool TryReadUuid(out Uuid value)` |

All methods advance the internal cursor on success.
TryReadString allocates a managed string (rare — only a few fields per handshake/login).
TryReadUuid returns a dedicated Uuid struct that preserves wire byte order, unlike System.Guid.

→ [Protocol layer](index.md)
