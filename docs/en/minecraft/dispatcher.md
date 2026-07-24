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

| Phase | packet id | Action |
|---|---|---|
| `Handshake` | `0x00` | `HandshakePacketParser.TryParse` → `_phase = Status` (on `nextState = Status`) |
| `Status` | `0x00` | `ServerStatusSerializer.Write` → framing → reply |
| `Status` | `0x01` | Reads `long timestamp` → writes Pong `[0x01][timestamp, BE]` → framing → reply |

Everything else (unknown packet id, phase mismatch) is the default branch: log + ignore.

## What is out of scope

The dispatcher does not drop the connection on a garbage packet — the `void OnPacket` contract does not let it say "drop." Today this is log + ignore; dropping is a separate step that would require changing the contract (e.g., a `bool` or `enum` return from `OnPacket`). Likewise, `nextState = Login` is logged while the phase stays Handshake: safe, because the client's next packet still falls into the default branch.

The Pong is written inline, without a dedicated serializer: the payload is trivial (`VarInt(0x01)` + 8 bytes big-endian), and a class for it would be a silver bullet. Reading Ping (one `long timestamp` field) is also inline. The asymmetry with Status Response (which has a serializer) is justified by different complexity.

→ [Minecraft layer](index.md)
