# Architecture

Map of the Verstack codebase: which projects exist, what each owns, and which way dependencies point. Implementation details of any layer live in their deep-dive pages.

## Solution layout

```
Verstack.slnx                          ← .NET 10 XML solution format
Directory.Build.props                  ← shared settings for all projects
src/
├── Verstack.App/                      ← Program.cs, entry point. AssemblyName=Verstack
├── Verstack.Bootstrap/                ← composition: ServerComposer + EntryPoint (main tick loop)
├── Verstack.Core/                     ← base abstractions: VerstackFeature, WorldScopes, ServerTime
├── Verstack.Debug/                    ← Logger (LogKey + LogLocale, i18n dictionary)
├── Verstack.ECS/                      ← vendored Leopotam.EcsProto + QoL. 0 NuGet
├── Verstack.NBT/                      ← NBT (planned, empty for now)
├── Verstack.Network/                  ← TCP/sockets + framing. Passive byte pump
├── Verstack.Layer.Global/             ← GLOBAL world: MOTD, ServerInfo, constants
├── Verstack.Layer.Gateway/            ← GATEWAY world: Handshake, Status, Login, Configuration
└── Verstack.Layer.Realm/              ← REALM world: Play phase (planned, empty for now)
tools/
└── Verstack.Probe/                    ← load-testing N-client simulator
```

## How dependencies run

```
                    App
                     │
                     ▼
                  Bootstrap
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   Layer.Realm   Network      Layer.Global
        │            │            │
        ▼            ▼            ▼
   Layer.Gateway  Verstack.ECS  Verstack.Core
        │            │            │
        ▼            ▼            ▼
   Layer.Global  (BCL only)    Verstack.Debug
        │                       (BCL only)
        ▼
   Layer.Global → Core → Debug
```

Dependencies are linear and point downward, toward the foundation. `App` is the composition root and the only executable assembly. `Bootstrap` assembles three ECS worlds out of Features and services and runs the main tick. `Layer.Realm → Layer.Gateway → Layer.Global → Core` is the layer pyramid: an upper layer knows the lower ones, never the reverse.

`Verstack.ECS` and `Verstack.Debug` are leaves: `ECS` depends only on BCL, `Debug` too. `Verstack.NBT` is empty for now and has no dependencies. `Verstack.Network` depends on `ECS` (the `RawPacket`/`PacketBundle` types use `ProtoEntity`) and `Debug` (logging).

| Layer             | May reference                                        | May NOT reference                           |
|-------------------|------------------------------------------------------|---------------------------------------------|
| `App`             | Bootstrap, ECS, Network                              | — (composition root)                        |
| `Bootstrap`       | Debug, ECS, NBT, Network, Core, Layer.Global/Gateway/Realm | — (assembly point)                          |
| `Layer.Realm`     | ECS, Core, Layer.Gateway                             | Network (directly), Layer.Global (transitively via Gateway) |
| `Layer.Gateway`   | ECS, Core, Layer.Global, Network                     | Layer.Realm                                 |
| `Layer.Global`    | ECS, Core                                            | Network, Layer.Gateway, Layer.Realm         |
| `Network`         | Debug, ECS                                           | layers, Core, Minecraft phases              |
| `Core`            | Debug, ECS                                           | layers, Network                             |
| `ECS` / `Debug` / `NBT` | BCL only                                       | anything application-level                  |

- **Layers never touch sockets directly.** The only path bytes take to the network is through a `NetworkChannel`, which a layer receives from `TcpNetworkService`. A bundle describes outgoing packets via `PacketOutbound` (a `ref struct` over heap buffers); framing and compression are the transport's concern, not the bundle's. Network knows nothing about Minecraft phases.
- **ECS is the foundation under the layers.** Vendored `Leopotam.EcsProto` (+QoL) lives in `Verstack.ECS`; every layer and Network depend on it. It is not thread-safe — synchronization is done by ECS systems (see the network decoupling below).

## ECS worlds and their visibility

Three isolated ECS worlds, one per logical scope. Names are constants in `WorldScopes`:

| Scope (`WorldScopes.*`) | Role                                                       | Sees other worlds            |
|------------------------|------------------------------------------------------------|------------------------------|
| `GLOBAL`               | Server-wide data: MOTD, ServerInfo, time                   | — (visible to everyone else) |
| `GATEWAY`              | Entry: Handshake, Status, Login, Configuration             | `GLOBAL`                     |
| `REALM`                | Game world: Play phase (planned)                           | `GLOBAL`, `GATEWAY`          |

Worlds are assembled in `ServerComposer`: each Feature (`GlobalFeature`, `GatewayFeature`, `RealmFeature`) registers its aspects (`ProtoAspectInject` stores) and systems. Services (`TcpNetworkService`, `ServerTime`) are added via `AddService` and injected with `[DI]` into every world. `AutoInjectModule(true)` enables injection into services too.

## The main tick

`EntryPoint.RunMainLoop` runs a fixed 20 TPS loop (`ServerConstants.TICK_INTERVAL = 1/20`):

```
while (_isRunning):
    try:
        globalSystems.Run()       # always: MOTD, time, metrics
        gatewaySystems.Run()      # can be paused (DDoS backpressure)
        # realmSystems.Run()      # always: Play phase, players don't notice the attack
    catch Exception:              # a tick must not crash the server — log and carry on
        Logger.Error(...)

    serverTime.Update()
    sleep until next tick (with instant wakeup on the stop signal)
```

The key idea of backpressure: under a DDoS attack on Gateway (`gatewaySystems.Run()` is skipped), the sockets in `TcpNetworkService` keep accepting packets and queueing them in the per-channel `ConcurrentQueue<RawPacket>`. When the pause lifts, the packets are drained. Realm keeps ticking the whole time, so in-game players don't notice the attack. EcsProto is not thread-safe, so the accept thread in `TcpNetworkService` **never touches** the world — it only pushes a `RawPacket` into a queue; the sole writer of the world is an ECS system in the main tick.

## The layers

### Verstack.Network

Passive byte pump. `TcpNetworkService` owns the listening socket and the accept loop: for each connection it creates a `NetworkChannel` (Socket + PipeReader/Writer + `ConcurrentQueue<RawPacket>`), pushes it into `PendingConnections`, and starts a background read. The read splits the byte stream into `RawPacket`s (packet id + payload) via `PacketFrame.TryRead` and enqueues them — with no Minecraft semantics whatsoever. `DataTypes/` holds encoding primitives (VarInt, Numeric, Utf8String, Uuid, PrefixedArray, etc.). `Packet/` holds framing and the pipeline skeleton: `PacketFrame`/`PacketFrameResult` (compression-aware framing), `PacketOutbound`/`SpanWriter` (GC-free outbound for bundles), `RawPacket`, `PacketBundle`, `PacketPipeline`, `PacketFlowState`. `Compression/` holds the `IPacketCompressor`/`IPacketDecompressor` abstractions and the default zlib implementations — framing switches to compressed format after a `Set Compression` per channel.

→ [Network](network/index.md)

### Verstack.Layer.Global

The GLOBAL world. `ServerInfoCacheStore` is an aspect with a dirty flag: MOTD/version/slots live as fields, and the status JSON is rebuilt only on change and cached as a `byte[]`. On a server-list ping — zero allocations, a ready array is returned. `UpdateServerInfoSystem` checks the dirty flag and rebuilds the cache once a second. `ServerTime` provides DeltaTime/TotalTime via `Stopwatch.GetTimestamp`, with no drift.

→ [Global](global/index.md)

### Verstack.Layer.Gateway

The GATEWAY world, the entry layer. `GuestScreeningSystem` takes new channels from `PendingConnections`, parses Handshake, and routes them: Status (ping/MOTD is served right here, no ECS entity) or Login (an ECS entity is created with `NetworkSession` + `PacketFlowState`). `PacketDispatchSystem` runs packets of logged-in sessions through `GatewayPacketPipeline` — a conveyor of `PacketBundle`s, where each bundle is a phase (Login, Configuration). `GatewayCacheStore` is an aspect: `Sessions`/`FlowStates` pools plus entity↔channel side-dictionaries.

→ [Gateway](gateway/index.md)

### Verstack.Layer.Realm

The REALM world, the Play phase. Reserved; `RealmFeature` is empty for now: `Init` with no systems, `GetCacheStores()` → `[]`. It will run at 20 TPS regardless of the load on Gateway.

### Verstack.Bootstrap

Composition. `ServerComposer` takes three Features, builds three `ProtoWorld`s out of their aspects (via `ProtoModules` + `AutoInjectModule`), registers services, and wires worlds by visibility. `EntryPoint` is the lifecycle: `Start(port)` initializes services and worlds, starts the TCP listener and the main tick; `Stop()` wakes the tick via a `CancellationToken`, stops the network, and destroys the worlds.

### Verstack.Core / Debug / ECS / NBT

`Core` — base abstractions: `VerstackFeature` (the Feature contract), `WorldScopes` (world names), `ServerTime`. `Debug` — `Logger` with i18n via `LogKey` + `LogLocale`. `ECS` — the vendored `Leopotam.EcsProto` (+QoL), the foundation under the layers. `NBT` — planned.

`Verstack.ECS` is the only third-party code in the project and is licensed under **MIT-ZARYA** ([LICENSE.md](../../src/Verstack.ECS/LICENSE.md)). MIT-ZARYA permits use and redistribution with one condition: if the software is localized into multiple languages, a Russian localization is mandatory and must be no less complete than any other. Verstack meets this — `docs/ru/` and `README.ru.md` mirror the English ones. The license file is included in the build output of `Verstack.ECS`.

## Current status

- ✅ ECS core: vendored Leopotam.EcsProto + QoL, three worlds (Global/Gateway/Realm), `AutoInjectModule`/`[DI]`.
- ✅ Main tick at 20 TPS with try/catch and instant stop on signal.
- ✅ Network: `TcpNetworkService` (accept → ConcurrentQueue), `NetworkChannel`, `PacketFrame` framing with `PacketOutbound` for bundles. Passive, thread/ECS decoupled.
- ✅ Gateway/Global: server-list ping answers with MOTD/version/slots via the GLOBAL cache at zero allocations.
- ✅ Handshake: parsing, populating `NetworkSession` with data (protocolVersion, IP, serverAddress, serverPort).
- ✅ Status: full ping exchange (Request → JSON Response, Ping → Pong) through the bundle conveyor, entity-backed.
- ✅ Login: offline-mode flow — Login Start → Set Compression → Login Success → Login Acknowledged. Offline UUID v3 from `"OfflinePlayer:<name>"`, protocol-776 Session ID field. Channel closes on phase completion (REALM/Configuration not implemented yet).
- ✅ Compression: zlib (RFC 1950) framing in both directions, per-channel threshold (256, vanilla standard), GC-free cold path. Enabled after Set Compression.
- 🔨 Configuration: not implemented — the layer does not yet handle packets after Login Acknowledged.
- 🔨 Realm: the Play phase is not implemented, the layer is empty.
