# Handshake

The Handshake phase is the entry point into the protocol: the first packet a client sends after connecting. It carries the protocol version, the address, the port, and the phase the client wants to transition to. The server parses it and switches the connection's phase; it sends no reply of its own.

## Packet types

| Type | Role |
|---|---|
| `HandshakePacket` | DTO — protocol version, address, port, next phase |
| `HandshakePacketParser` | Parser — Handshake payload → DTO |

The DTO is a `readonly struct` — inert fields. `HandshakeNextState` is a separate enum (`Status = 1`, `Login = 2`) that captures the values of the wire protocol: what the client *may request*, not what the server *serves*. This distinction matters: the server does not yet implement Login, but `HandshakeNextState.Login` is still a valid wire value, and the parser accepts it.

The parser is a `static class` that reads the packet body via `PacketReader` (after the packet id, which the dispatcher consumes). It validates `nextState` at the parsing boundary: a value outside `{1, 2}` is a malformed client, and the parser returns `false`.

## Wire format

```
Handshake (client → server):  [0x00][VarInt(protoVersion)][VarInt(len)][UTF-8 address][ushort port, BE][VarInt(nextState)]
                                 ↑ packet id 0x00 in the Handshake phase
```

Fields:

| Field | Type | Example |
|---|---|---|
| protocolVersion | VarInt | `774` for 1.21.6 |
| serverAddress | length-prefixed UTF-8 | `localhost` |
| serverPort | ushort, big-endian | `25565` |
| nextState | VarInt | `1` = Status, `2` = Login |

`serverAddress` and `serverPort` are informational — what the client connected to (an SRV record, for instance, may point at a different port). The server does not use them yet, but parses and keeps them in the DTO for future phases (Login validates them).

## Switching the phase

The dispatcher takes the parsed `HandshakePacket` and switches the connection's phase based on `NextState`. How exactly is in [Dispatcher](dispatcher.md). The fact fixed here: `nextState = Status` transitions to the Status phase; `nextState = Login` is not implemented yet and is logged, with the phase remaining Handshake.

→ [Minecraft layer](index.md)
