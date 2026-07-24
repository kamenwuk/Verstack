# Status

The Status phase answers server-list pings. A client connects, sends Handshake (with `next state = Status`), then Status Request; the server replies with Status Response, a JSON payload describing the server. A Ping may follow, which the server answers with a Pong carrying the same timestamp.

## Packet types

| Type | Role |
|---|---|
| `ServerStatusResponse` | DTO — version, capacity, MOTD |
| `ServerStatusSerializer` | Serializer — DTO → Status Response payload |

The DTO is a `readonly struct` — inert fields, zero dependencies. Nested DTOs hold the parts: `ServerVersion` (name + protocol number), `ServerCapacity` (max slots + online count).

The serializer is a `static class` that encodes a DTO into the packet payload — `[VarInt(packetId)][VarInt(jsonLen)][UTF-8 JSON]` — producing the payload only; framing is `PacketFraming`'s job. There is no parser for Status Response: the server sends it, never receives it.

## Wire format

```
Status Request (client → server):  [0x00]                           ← packet id 0x00, empty payload
Status Response (server → client): [0x00][VarInt(jsonLen)][JSON]    ← packet id 0x00, JSON body
Ping (client → server):            [0x01][long timestamp, BE]       ← packet id 0x01
Pong (server → client):            [0x01][long timestamp, BE]       ← echo of the same timestamp
```

The JSON body of Status Response:

```json
{
  "version":     { "name": "1.21.6", "protocol": 774 },
  "players":     { "max": 20, "online": 0 },
  "description": { "text": "A Minecraft Server" }
}
```

Ping and Pong are a request/response pair of the Status phase. The server reads the timestamp out of Ping and writes it back into Pong; the client measures the ping shown in the server list from the difference. How these packets are dispatched is in [Dispatcher](dispatcher.md).

## Serialization style

The serializer writes into an `IBufferWriter<byte>` (a scratch buffer), never returns a `byte[]` — this matches `PacketFraming.Write` and avoids a copy. Serialization is two-phase: the JSON body is written into a scratch buffer with `Utf8JsonWriter` (the length isn't known until the body is written), then the final payload is written into the output in one contiguous span. One allocation on a cold path (status pings are rare); hot-path packet serialization will avoid the scratch buffer with a measure-then-write pass.

→ [Minecraft layer](index.md)
