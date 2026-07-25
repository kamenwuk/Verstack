# Architecture

Map of the Verstack codebase: which projects exist, what each owns, and which way dependencies may point. Implementation details of any layer live in their deep-dive pages.

## Solution layout

```
Verstack.slnx                          ← .NET 10 XML solution format
Directory.Build.props                  ← shared settings for all projects
src/
├── Verstack.Network/                  ← TCP/sockets + PipeReader loop. Depends on Protocol.
├── Verstack.Protocol/                 ← VarInt, framing, field reading. Pure logic, 0 NuGet deps.
├── Verstack.Minecraft/                ← Minecraft packet semantics. Depends on Protocol and Network.
│   ├── Handshake/                     ← Handshake phase: DTO, parser.
│   ├── Status/                        ← Status phase: DTO, serializer.
│   └── Session/                       ← session infrastructure: phase, dispatcher, factory.
└── Verstack.App/                      ← Program.cs, entry point. AssemblyName=Verstack
tests/
├── Verstack.Protocol.Tests/           ← xUnit, exercises Protocol via Span/Sequence
├── Verstack.Minecraft.Tests/          ← xUnit, exercises Minecraft serialization via IBufferWriter
└── Verstack.Network.Tests/            ← xUnit, exercises SessionLifetime's read loop via a pair of Pipes (no socket)
```

## How dependencies run

```
App  ──►  Network  ──►  Protocol  ──►  (BCL only)
 │          ▲
 │          │ Minecraft implements IPacketHandler
 └────►  Minecraft  ──►  Protocol  ──►  (BCL only)
```

The dependency is linear, not symmetric: Minecraft references Network, never the other way around. This is Dependency Inversion — Network owns the `IPacketHandler` contract ("how I hand a parsed frame to the layer above") and the `IPacketHandlerFactory` contract ("how I get a handler for each connection"), and Minecraft provides implementations of both. The layering rule here is *know less, not more*: Network still knows nothing about Minecraft packets.

All arrows point downward, toward Protocol / BCL. Protocol is the foundation everyone builds on, and it references only the base class library.

| Layer       | May reference                                             | May NOT reference            |
|-------------|-----------------------------------------------------------|------------------------------|
| `App`       | Network, Minecraft, Protocol                              | — (composition root)         |
| `Network`   | Protocol, the `IPacketHandler`/`IPacketHandlerFactory` contracts | Minecraft packet specifics   |
| `Minecraft` | Network (`IPacketHandler`/`IPacketHandlerFactory`), Protocol | — (upper layer)              |
| `Protocol`  | BCL only (`System.Buffers`)                               | Sockets, Network, Minecraft  |

- **Protocol never references Network or Minecraft.** Tested in isolation via `Span<byte>` / `ReadOnlySequence<byte>`, no socket.
- **Network never references Minecraft.** The only path from Minecraft into Network's world is the `IPacketHandler`/`IPacketHandlerFactory` implementation that App plugs in.

## The layers

### Verstack.Network

TCP sockets and the `PipeReader`/`PipeWriter` loops that turn a raw byte stream into framed Minecraft payloads and back. Built on `Pipelines.Sockets.Unofficial` (raw sockets + pipe, by Marc Gravell). Owns `TcpServer`, `SessionLifetime`, and the `IPacketHandler`/`IPacketHandlerFactory` contracts.

→ [Network](network/index.md)

### Verstack.Protocol

Pure logic, no NuGet dependencies. Everything here works on `Span<byte>` and `ReadOnlySequence<byte>`. Provides `VarInt` (LEB128 integers), the framing pair — `PacketFrameScanner` reads frames out of a sequence, `PacketFraming` writes them into a buffer — and `PacketReader`, which reads the fields of one packet from a complete frame's payload.

→ [Protocol](protocol/index.md)

### Verstack.Minecraft

Where bytes become Minecraft. Packet DTOs, their serializers and parsers, organized by protocol phase (`Handshake`, `Status` today; `Login` and `Play` to come), plus the dispatcher that routes frames by `(phase, packet id)`. References Network only to implement `IPacketHandler`/`IPacketHandlerFactory`.

→ [Minecraft](minecraft/index.md)

### Verstack.App

Entry point (`Program.cs`) and composition root: constructs the status data and a `PacketDispatcherFactory` (Minecraft), hands it to a `TcpServer` (Network), and runs the server until Ctrl+C.

## Current status

- ✅ TCP listener on 25565, accepts connections.
- ✅ Reads and frames incoming packets (`PacketFrameScanner`).
- ✅ Writes framed outbound packets (`PacketFraming`).
- ✅ Handshake state machine: parses Handshake, switches phase, dispatches by packet id (Status Request → Status Response, Ping → Pong).
- ✅ Status Response: a real Minecraft 1.21.6 client pinging the server list sees the MOTD, version, and player slots.
- ⬜ Login — the next protocol phase: authentication, encryption, compression.
- ✅ Concurrency — the accept loop is not blocked by a session: each connection is serviced in a background task, and on shutdown the server awaits stragglers via `Task.WhenAll`.
- ✅ Dropping the connection on a garbage packet — the handler returns `PacketVerdict.Disconnect`, and `SessionLifetime` tears the connection down after the flush.
