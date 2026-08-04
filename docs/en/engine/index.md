# Engine

The Verstack engine. Four projects under the server's hood: ECS, network transport, the Network↔ECS decoupling, and
server composition. None of them know about Minecraft phases — that's the [layers'](../layers/index.md) job.

```text
Verstack.Engine.Ecs       ← vendored Leopotam.EcsProto
Verstack.Engine.Network   ← transport (sockets, framing, compression)   → see network.md
Verstack.Engine.Bridge    ← Network↔ECS decoupling (channel routing)    → see bridge.md
Verstack.Engine.Lifecycle ← server composition, tick loop (this file)
```

## Engine.Ecs

Vendored `Leopotam.EcsProto` + QoL (`src/engine/Verstack.Engine.Ecs/`). DOD, GC-free, `ref struct` iterators without
`IEnumerable`, aspects (`ProtoAspectInject`), pools (`ProtoPool<T>`), DI (`AutoInjectModule`, `[DI]`). Not thread-safe —
synchronization between threads is done by systems and queues. 0 NuGet, BCL only. License MIT-ZARYA (see README).

Vendored, not NuGet — to keep a single style and the project's conventions; EcsProto has no official package. Vendored
code is modified minimally; EcsProto conventions (Russian exception texts, `#if DEBUG`) are preserved.

## Engine.Network

Transport: TCP/sockets, framing, compression. Knows nothing about Minecraft phases — only bytes and frames. Full
breakdown — in [network.md](network.md). For this file, one junction with Bridge matters (below).

## Engine.Bridge

Decoupling of the async network layer and the synchronous ECS tick: channel routing between layers, the player's state
machine (`Pending → Connected → Disconnected`). Full breakdown — in [bridge.md](bridge.md).

## Engine.Lifecycle

Server composition and the main loop. Contains:

- `ServerFeatureLayer` — abstract extension point; each layer (Global/Gateway/Realm) inherits from it.
- `ServerComposer` — builds an array of `ProtoSystems` (one per world) from `ServerFeatureLayer`s.
- `EntryPoint` — the main tick loop (20 TPS).
- `ServerTime` — `DeltaTime`/`TotalTime` on `Stopwatch`, without accumulating drift.
- `ServerConstants` — TPS, tick duration, compression threshold.
- `ServerWorldScopes` — string constants of world names (`GLOBAL`/`GATEWAY`/`REALM`).

### ServerFeatureLayer

Abstract class that each layer implements. Through it the Composer gets everything needed to build the world:

| Member | Purpose |
|--------|---------|
| `Scope` | Name of the layer's ECS world (from `ServerWorldScopes`) |
| `Init(IProtoSystems)` | Final initialization after world assembly |
| `GetCacheStores()` | Layer aspects (`ProtoAspectInject[]`) — caches with component pools and filters |
| `GetVisibleScopes(...)` | Foreign world scopes the layer wants to see (to read their data) |
| `GetNextScope()` | Scope of the layer this one hands the player off to (or empty if nowhere) |
| `GetHandoffPolicy()` | Transfer policy for Bridge (or `null` if the layer doesn't hand off) |

How a layer author implements this class (their bundles, systems, caches) is in [layers/index.md](../layers/index.md).
Here the mechanism matters: the Composer queries these methods during assembly.

### ServerComposer.Compose

Assembly runs in three phases. The layer order is fixed on input: Global always first, then Gateway, Realm.

**Phase 1 — building the Global world.** Global is built separately: it receives `NetworkHubModule` in `BuildSystems` —
a module that registers `TcpNetworkService` (`internal sealed`, hidden inside the module) and compressors
(`IPacketCompressor`/`IPacketDecompressor`) as services. After assembly these services land in the shared list and, via
`[DI(ServerWorldScopes.GLOBAL)]`, become visible to systems of all subsequent worlds. Global is the only layer through
which Network enters composition: it acts as the service registration point, not the "owner of sockets."

**Phase 2 — building the Gateway and Realm worlds.** Each receives `BridgeLayerModule(scope, nextScope, handoffRouter,
handoffPolicy)` instead of `NetworkHubModule` — the Bridge module that installs four systems in fixed order (see
[bridge.md](bridge.md)). Thus the layer gets access to Bridge, but not to `TcpNetworkService` directly: the only thing
that reaches game systems from Network is the compression services.

**Phase 3 — visibility setup.** For each layer the Composer queries `GetVisibleScopes` and registers the requested
foreign worlds via `sys.AddWorld(foreignWorld, scope)`. The layer's own world is already registered under its `Scope` name.
If a layer requests a world that isn't among the registered ones — an exception. This is what enforces flat visibility:
Gateway and Realm never request each other's worlds, only `GLOBAL`.

**Phase 4 — initialization.** `layer.Init(systems)` is called on each assembled `ProtoSystems` — final layer bring-up
after worlds and visibility are ready.

### Network ↔ Bridge junction

Network and Bridge are decoupled through the `ClientLifecycleHandler` abstraction (in `Engine.Network`): two methods —
`HandleConnect(channel)` and `HandleDisconnect(channel)`. `TcpNetworkService` calls them from its background threads on
socket accept and on disconnect.

The only implementation is `BridgeHandoffRouter` (in `Engine.Bridge`). One instance per server, passed both into
`NetworkHubModule` (as the handler) and into each `BridgeLayerModule` (as the router). Thus Network knows nothing about
Bridge and ECS — it only knows the handler interface; and Bridge receives channel lifecycle events without direct access
to `TcpNetworkService`. Routing details — in [bridge.md](bridge.md).
