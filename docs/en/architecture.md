# Architecture

Map of the Verstack codebase: which projects exist, what each owns, and which way dependencies may point. Implementation details of any layer live in their deep-dive pages.

## Solution layout

```
Verstack.slnx                          ← .NET 10 XML solution format
Directory.Build.props                  ← shared settings for all projects
src/
├── Verstack.Network/                  ← TCP/sockets + PipeReader loop. Depends on Protocol.
├── Verstack.Protocol/                 ← VarInt, framing. Pure logic, 0 NuGet deps.
├── Verstack.Minecraft/                ← Minecraft packet semantics. Depends on Protocol and Network.
│   └── Status/                        ← Status phase: DTO, serializer, handler.
└── Verstack.App/                      ← Program.cs, entry point. AssemblyName=Verstack
tests/
├── Verstack.Protocol.Tests/           ← xUnit, exercises Protocol via Span/Sequence
└── Verstack.Minecraft.Tests/          ← xUnit, exercises Minecraft serialization via IBufferWriter
```

## How dependencies run

```
App  ──►  Network  ──►  Protocol  ──►  (BCL only)
 │          ▲
 │          │ Minecraft implements IPacketHandler
 └────►  Minecraft  ──►  Protocol  ──►  (BCL only)
```

The dependency is linear, not symmetric: Minecraft references Network, never the other way around. This is Dependency Inversion — Network owns the `IPacketHandler` contract ("how I hand a parsed frame to the layer above"), and Minecraft provides an implementation of it. The layering rule here is *know less, not more*: Network still knows nothing about Minecraft packets.

All arrows point downward, toward Protocol / BCL. Protocol is the foundation everyone builds on, and it references only the base class library.

| Layer       | May reference                            | May NOT reference            |
|-------------|------------------------------------------|------------------------------|
| `App`       | Network, Minecraft, Protocol             | — (composition root)         |
| `Network`   | Protocol, the `IPacketHandler` contract  | Minecraft packet specifics   |
| `Minecraft` | Network (`IPacketHandler`), Protocol     | — (upper layer)              |
| `Protocol`  | BCL only (`System.Buffers`)              | Sockets, Network, Minecraft  |

- **Protocol never references Network or Minecraft.** Tested in isolation via `Span<byte>` / `ReadOnlySequence<byte>`, no socket.
- **Network never references Minecraft.** The only path from Minecraft into Network's world is the `IPacketHandler` implementation that App plugs in.

## The layers

### Verstack.Network

TCP sockets and the `PipeReader`/`PipeWriter` loops that turn a raw byte stream into framed Minecraft payloads and back. Built on `Pipelines.Sockets.Unofficial` (raw sockets + pipe, by Marc Gravell). Owns `TcpServer`, `SessionLifetime`, and the `IPacketHandler` contract.

→ [Network](network/index.md)

### Verstack.Protocol

Pure logic, no NuGet dependencies. Everything here works on `Span<byte>` and `ReadOnlySequence<byte>`. Provides `VarInt` (LEB128 integers) and the framing pair — `PacketFrameScanner` reads frames out of a sequence, `PacketFraming` writes them into a buffer.

→ [VarInt](protocol/varint.md), [Packet Framing](protocol/packet-framing.md)

### Verstack.Minecraft

Where bytes become Minecraft. Packet DTOs, their serializers, and the handlers that react to them, organized by protocol state (`Status` today; `Login` and `Play` to come). References Network only to implement `IPacketHandler`.

→ [Minecraft](minecraft/index.md)

### Verstack.App

Entry point (`Program.cs`) and composition root: constructs the status data and a `ServerStatusHandler` (Minecraft), hands it to a `TcpServer` (Network), and runs the server until Ctrl+C.

## Current status

- ✅ TCP listener on 25565, accepts connections.
- ✅ Reads and frames incoming packets (`PacketFrameScanner`).
- ✅ Writes framed outbound packets (`PacketFraming`).
- ✅ Status Response: a real Minecraft 1.21.6 client pinging the server list sees the MOTD, version, and player slots.
- ⬜ Handshake state machine — parse the Handshake packet, switch protocol state, dispatch by packet id.
- ⬜ Real dispatcher — `ServerStatusHandler` currently answers *any* frame with a status response (a deliberate stub); it needs to answer Status Request only and echo Ping back as Pong.
