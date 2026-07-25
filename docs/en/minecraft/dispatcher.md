# Dispatcher

The dispatcher is the point where a single frame's payload turns into a server reaction. It reads the packet id from the start of the payload and routes by `(phase, packet id)` to the right action. It is a per-connection object: every connection gets its own dispatcher with its own phase.

Three types carry this responsibility: `SessionPhase`, `PacketDispatcher`, `PacketDispatcherFactory`.

## Phase per connection

`SessionPhase` is an enum of protocol phases (`Handshake`, `Status`; `Login`, `Play` will be added). The values are ordered so that `default(SessionPhase) = Handshake`: a freshly created dispatcher is already in the starting phase without an explicit initializer.

The phase is an instance field of the dispatcher, isolated between connections. It is the only mutable state per connection, and the reason the factory exists: a single handler cannot be shared across all clients — each goes through its own path Handshake → Status → (Login → Play). See [the factory contract](../network/packet-handler.md).

## The factory

`PacketDispatcherFactory` holds the data shared across all connections (the server status, immutable) and produces a fresh `PacketDispatcher` with its own phase in `Create()`. `TcpServer` calls `Create()` in the accept loop for each connection.

The dispatcher owns an `ArrayBufferWriter<byte>` as a field: one buffer allocation per connection, reused across Status frames via `Clear()`. The handler lives as long as the connection; the buffer lives as long as the handler.

## Routing

The dispatcher implements `IPacketHandler.OnPacket`. Internally:

1. It creates a `PacketReader` over the frame's payload.
2. It reads the packet id (VarInt) itself, not via a delegate — because routing switches on it.
3. A `switch (_phase, packetId)` picks the action.

The Status-phase routing table:

| Phase | packet id | Action | Verdict |
|---|---|---|---|
| `Handshake` | `0x00` | `HandshakePacketParser.TryParse` → `_phase = Status` (on `nextState = Status`) | `Keep` (or `Disconnect` if the body fails to parse) |
| `Status` | `0x00` | `ServerStatusSerializer.Write` → framing → reply | `Keep` |
| `Status` | `0x01` | Reads `long timestamp` → writes Pong `[0x01][timestamp, BE]` → framing → reply | `Keep` (or `Disconnect` if the timestamp is missing) |

Everything else (no packet id, unknown packet id, phase mismatch) is the default branch: log + `Disconnect`. `SessionLifetime` honors the verdict [after the flush](../network/server-lifetime.md).

## What gets dropped, what does not

The dispatcher drops the connection on frames it cannot handle: a frame without a packet id, a Handshake/Ping body that fails to parse, or a packet id not valid for the current phase. These are garbage — a legitimate client never sends them. Dropping immediately means the client gets fast feedback and the server does not spend cycles continuing a dialog with a broken peer.

`nextState = Login` is the **exception**: it is a valid Handshake with an unimplemented `nextState`. Dropping here would tear down legitimate clients trying to log in. It is logged, the phase stays Handshake, the verdict is `Keep`. The client's next packet still falls into the default branch and gets dropped anyway.

The Pong is written inline, without a dedicated serializer: the payload is trivial (`VarInt(0x01)` + 8 bytes big-endian), and a class for it would be a silver bullet. Reading Ping (one `long timestamp` field) is also inline. The asymmetry with Status Response (which has a serializer) is justified by different complexity.

→ [Minecraft layer](index.md)
