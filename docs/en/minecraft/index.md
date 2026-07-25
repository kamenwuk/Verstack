# The Minecraft layer

Minecraft is where bytes become Minecraft. Packet DTOs, their serializers and parsers, plus the dispatcher that routes frames by protocol phase, live here. The layer is organized by protocol phase, because Minecraft's wire format depends on the current phase: packet id `0x00` means different things in Handshake, Status, Login, and Play, and their DTOs must not collide.

Today the Handshake and Status phases are fully implemented; the Login phase is partial (entry into the phase and Login Start parsing, without the encryption/compression exchange and Login Success). Play will follow.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## Organization

```
Verstack.Minecraft/
├── Handshake/              ← the Handshake phase (DTO, parser)
├── Status/                 ← the Status phase (DTO, serializer)
├── Login/                  ← the Login phase (DTO, parser — partial)
└── Session/                ← session infrastructure (phase, dispatcher, factory)
```

Each phase is a folder under the project root, mirroring its namespace (`Verstack.Minecraft.Status`). `Session/` is separate: it is not a phase but the framework shared by all phases — the connection phase, the `(phase, packet id)` dispatcher, and the factory that creates a dispatcher per connection. It belongs to no single phase, because every phase uses it.

## Packet types

Each packet type in a phase is expressed through cooperating types, keeping data and encoding separate so each can be tested in isolation:

- **DTO** — a `readonly struct`. Inert fields, zero dependencies.
- **Serializer** — a `static class`. Encodes a DTO into the packet payload (`VarInt` + fields); produces the payload only, framing is `PacketFraming`'s job. This is the write side.
- **Parser** — a `static class`. Decodes a packet payload into a DTO via `PacketReader`; reads the body after the packet id, which the dispatcher has already consumed. This is the read side, the mirror of the serializer.

Not every packet has both sides: the client's Status Request is empty — it has no DTO; the server's Status Response has a serializer but no parser (the server never receives it). Handshake has only a parser (the server receives it, never sends it). The serializer/parser pair appears where a packet travels both ways (Login, Play).

→ [Handshake](handshake.md)
→ [Status](status.md)
→ [Login](login.md)
→ [Dispatcher](dispatcher.md)
