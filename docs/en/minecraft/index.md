# The Minecraft layer

Minecraft is where bytes become Minecraft. Packet DTOs, their serializers, and the handlers that react to them live here. The layer is organized by protocol state, because Minecraft's wire format depends on the current state: packet id `0x00` means different things in Status, Login, and Play, and their DTOs must not collide.

Today only the Status state exists (`Status/`); Login and Play will follow the same layout as they land.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## Organization

```
Verstack.Minecraft/
└── Status/                    ← the Status state
    ├── ServerStatusResponse.cs       ← DTO: version, capacity, MOTD
    ├── ServerVersion.cs              ← nested DTO: version name + protocol number
    ├── ServerCapacity.cs             ← nested DTO: max slots + online count
    ├── ServerStatusSerializer.cs     ← DTO → Status Response payload
    └── ServerStatusHandler.cs        ← IPacketHandler for the Status state
```

## The three actors

Each packet type is expressed through three cooperating types, keeping data, encoding, and behavior separate so each can be tested in isolation.

The **DTO** is a `readonly struct` — inert fields, zero dependencies. The **serializer** is a `static class` that encodes a DTO into a packet payload (`VarInt` + fields); it produces the payload only, framing is `PacketFraming`'s job. The **handler** is the `IPacketHandler` implementation that reacts to incoming payloads and writes responses; it is the only actor that touches the connection.

For the Status state these are `ServerStatusResponse`, `ServerStatusSerializer`, and `ServerStatusHandler`. The handler composes the other two:

```
ServerStatusSerializer.Write(scratch, in status)   → payload bytes
PacketFraming.Write(writer, scratch.WrittenSpan)   → framed bytes into the PipeWriter
SessionLifetime: await writer.FlushAsync(token)    → onto the socket
```

Serializing into a scratch buffer first is necessary because `PacketFraming` needs the payload as a contiguous span, while the serializer writes it directly into a buffer — so the payload is staged in a scratch `ArrayBufferWriter<byte>`, then framed into the connection. One allocation per outbound packet; pooled via `ArrayPool` later.

## Serialization style

Serializers write into an `IBufferWriter<byte>` (a scratch buffer, or a `PipeWriter` for already-framed data), never return a `byte[]`. This matches `PacketFraming.Write` and avoids a copy. For JSON payloads like Status Response, serialization is two-phase: the JSON body is written into a scratch buffer with `Utf8JsonWriter` (the length isn't known until the body is written), then the final payload — `[VarInt(packetId)][VarInt(jsonLen)][JSON]` — is written into the output in one contiguous span. This is one allocation on a cold path (status pings are rare); hot-path packet serialization will avoid the scratch buffer with a measure-then-write pass.

## Current state: a stub handler

`ServerStatusHandler` replies with the configured status to **any** incoming frame, without parsing Handshake, tracking protocol state, or distinguishing packet ids. This is enough to reach the first visible result — a MOTD in the server list — and defers the real machinery to a later milestone.

What replaces the stub: a Handshake parser that reads protocol version, server address, port, and `next state`, switching the connection into Status or Login; a state machine that tracks the current state and selects the active handler; and per-packet-id dispatching within a state (Status Request → Status Response, Ping → Pong).
