# Architecture

Map of Verstack projects and dependency direction. Implementation details live in deep-dives by group:
[engine](engine/index.md), [layers](layers/index.md), [shared](shared/index.md). Code conventions are in
[conventions.md](conventions.md).

## Project groups

Sources (`src/`) are split into four groups by role. The solution is `Verstack.slnx` (.NET 10 XML format).

**engine/** — the engine. Knows nothing about Minecraft phases: sockets, framing, ECS, server composition, tick loop.

| Project | Role |
|---------|------|
| `Verstack.Engine.Ecs` | Vendored `Leopotam.EcsProto` + QoL. 0 NuGet, BCL only |
| `Verstack.Engine.Lifecycle` | Server composition: `ServerFeatureLayer`, `ServerComposer`, `EntryPoint` (tick loop), `ServerTime`, `ServerWorldScopes`, `ServerConstants` |
| `Verstack.Engine.Network` | TCP/sockets, framing, compression. Passive byte pump |
| `Verstack.Engine.Bridge` | Network↔ECS decoupling: channel routing between layers, player state machine |

**layers/** — Minecraft phase layers on ECS. Each sees only Global; Gateway↔Realm coupling goes through Bridge.

| Project | Role |
|--------|------|
| `Verstack.Layers.Global` | GLOBAL world: `ServerInfo`, `SyncedRegistryCatalog`, owner of Assets. Visible to all, itself knows nobody |
| `Verstack.Layers.Gateway` | GATEWAY world: Status, Login, Configuration. Entry layer |
| `Verstack.Layers.Realm` | REALM world: Play phase (Join, Movement) |

**shared/** — reusable subsystems without phase logic. Do not depend on the engine or layers.

| Project | Role |
|--------|------|
| `Verstack.Shared.Debug` | `Logger` (`LogKey` + `LogLocale`, i18n dictionary) |
| `Verstack.Shared.Nbt` | NBT reader/writer (`ref struct`, modified UTF-8, networked-root) |
| `Verstack.Shared.Assets` | Loader for compiled binary assets (`AssetCatalog`, cache buffers) |

**tools/** — data build utilities, not part of runtime.

| Project | Role |
|--------|------|
| `Verstack.Tools.DataCompiler` | Compiler of vanilla JSON → binary `.registry`/`.tags`/`.nbt` into `App/assets/` |

Tests and benchmarks live next to the projects in `!tests/` and `!benchmark/` (the `!` prefix keeps them at the bottom of
the IDE list).

## Dependency graph

The arrow `A → B` means "A references B".

```text
Verstack.App
  ├─→ Verstack.Engine.Lifecycle
  ├─→ Verstack.Layers.Global
  ├─→ Verstack.Layers.Gateway
  ├─→ Verstack.Layers.Realm
  └─→ Verstack.Shared.Assets

Verstack.Engine.Lifecycle ─→ Verstack.Engine.Bridge
                           └─→ Verstack.Engine.Network
Verstack.Engine.Bridge     ─→ Verstack.Engine.Network
Verstack.Engine.Network    ─→ Verstack.Engine.Ecs
Verstack.Engine.Ecs        ─→  (nothing, BCL only)

Verstack.Layers.Global  ─→ Verstack.Engine.Lifecycle
Verstack.Layers.Gateway ─→ Verstack.Engine.{Bridge, Ecs, Lifecycle, Network}
                         └─→ Verstack.Shared.{Assets, Nbt}
                         └─→ Verstack.Layers.Global
Verstack.Layers.Realm   ─→ Verstack.Engine.Ecs
                         └─→ Verstack.Layers.Global

Verstack.Shared.{Debug, Nbt, Assets} ─→  (nothing, BCL only)
```

All engine projects depend on `Verstack.Shared.Debug` (logging). `Shared.*` are leaves of the graph, depending on nobody.

Layers do not uniformly repeat the set of engine dependencies: each takes exactly what it needs. Global — only Lifecycle
(it doesn't need Network and Bridge, it doesn't work with sockets). Realm — Engine.Ecs + Global (phase logic over ECS;
network intake is delegated to Bridge, which Realm has no direct access to).

## Worlds and visibility

Three ECS worlds by scope — `GLOBAL`, `GATEWAY`, `REALM` (constants in `ServerWorldScopes`). Visibility is **flat**:

- `GLOBAL` is visible to all layers, but Global itself knows nobody.
- `GATEWAY` and `REALM` **do not see each other** — neither in ECS nor in project dependencies.

The connection between Gateway and Realm goes **only through Bridge** — it transfers ownership of the player's channel
from layer to layer. How Bridge works (router, states, system order on the tick) is in [engine/bridge.md](engine/bridge.md).
How a layer declares its scope, next scope, and visibility is in [layers/index.md](layers/index.md).

## Tick loop

`EntryPoint.Start` creates a `ServerComposer`, builds an array of `ProtoSystems` from `ServerFeatureLayer`s (one per
layer), calls `Init()` on each, and starts the main loop. Each tick: `layer.Run()` on all layers in turn, then
`ServerTime.Update()`, then sleep until the end of the tick (20 TPS, 50 ms). Stop — on `Ctrl+C` or console close.

World composition (visibility, registration of foreign worlds) is performed in `ServerComposer.Compose` in three phases:
world creation → visibility setup → layer initialization. Details — in [engine/index.md](engine/index.md).
