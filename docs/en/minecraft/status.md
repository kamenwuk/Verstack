# Status

The Status state answers server-list pings. A client connects, sends Handshake (with `next state = Status`), then Status Request; the server replies with Status Response, a JSON payload describing the server. The current implementation reaches this result with a deliberate shortcut — see [Current state](#current-state) below.

## The three actors

| Actor                  | Type                                                                |
|------------------------|---------------------------------------------------------------------|
| DTO                    | `ServerStatusResponse` — version, capacity, MOTD                    |
| Serializer             | `ServerStatusSerializer` — DTO → Status Response payload            |
| Handler                | `ServerStatusHandler` — `IPacketHandler` for the Status state       |

The DTO is a `readonly struct` — inert fields, zero dependencies. Nested DTOs hold the parts: `ServerVersion` (name + protocol number), `ServerCapacity` (max slots + online count).

The serializer is a `static class` that encodes a DTO into the packet payload — `[VarInt(packetId)][VarInt(jsonLen)][UTF-8 JSON]` — producing the payload only; framing is `PacketFraming`'s job. The handler is the `IPacketHandler` implementation that reacts to incoming payloads and writes responses; it composes the other two:

```
ServerStatusSerializer.Write(scratch, in status)   → payload bytes
PacketFraming.Write(writer, scratch.WrittenSpan)   → framed bytes into the PipeWriter
SessionLifetime: await writer.FlushAsync(token)    → onto the socket
```

Serializing into a scratch buffer first is necessary because `PacketFraming` needs the payload as a contiguous span, while the serializer writes it directly into a buffer — so the payload is staged in a scratch `ArrayBufferWriter<byte>`, then framed into the connection. One allocation per outbound packet; pooled via `ArrayPool` later.

## Wire format

```
Status Request (client → server):  [0x00]                           ← packet id 0x00, empty payload
Status Response (server → client): [0x00][VarInt(jsonLen)][JSON]    ← packet id 0x00, JSON body
```

The JSON body:

```json
{
  "version":     { "name": "1.21.6", "protocol": 774 },
  "players":     { "max": 20, "online": 0 },
  "description": { "text": "A Minecraft Server" }
}
```

## Serialization style

The serializer writes into an `IBufferWriter<byte>` (a scratch buffer), never returns a `byte[]` — this matches `PacketFraming.Write` and avoids a copy. Serialization is two-phase: the JSON body is written into a scratch buffer with `Utf8JsonWriter` (the length isn't known until the body is written), then the final payload is written into the output in one contiguous span. One allocation on a cold path (status pings are rare); hot-path packet serialization will avoid the scratch buffer with a measure-then-write pass.

## Current state

`ServerStatusHandler` is a stub: it replies with the configured status to **any** incoming frame, without parsing Handshake, tracking protocol state, or distinguishing packet ids. This is enough to reach the first visible result — a MOTD in the server list — and defers the real machinery to a later milestone: a Handshake parser that switches the connection into Status or Login, a state machine that selects the active handler, and per-packet-id dispatching (Status Request → Status Response, Ping → Pong).

→ [Minecraft layer](index.md)
