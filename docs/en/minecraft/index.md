# The Minecraft layer

Minecraft is where bytes become Minecraft. Packet DTOs, their serializers, and the handlers that react to them live here. The layer is organized by protocol state, because Minecraft's wire format depends on the current state: packet id `0x00` means different things in Status, Login, and Play, and their DTOs must not collide.

Today only the Status state exists; Login and Play will follow the same layout as they land.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## Organization

```
Verstack.Minecraft/
└── Status/                    ← the Status state
```

Each state is a folder under the project root, mirroring its namespace (`Verstack.Minecraft.Status`).

## The three actors

Each packet type in a state is expressed through three cooperating types, keeping data, encoding, and behavior separate so each can be tested in isolation:

- **DTO** — a `readonly struct`. Inert fields, zero dependencies.
- **Serializer** — a `static class`. Encodes a DTO into the packet payload (`VarInt` + fields); produces the payload only, framing is `PacketFraming`'s job.
- **Handler** — an `IPacketHandler` implementation. Reacts to incoming payloads and writes responses; the only actor that touches the connection.

→ [Status](status.md)
