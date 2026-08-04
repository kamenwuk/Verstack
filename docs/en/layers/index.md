# Layers

Minecraft phase layers on ECS. Each layer is a `ServerFeatureLayer` (the engine's extension point) with its own ECS
world, its own systems and cache stores. Layers see only [Global](global.md); Gateway↔Realm coupling goes through
[Bridge](../engine/bridge.md).

```text
Verstack.Layers.Global   ← visible to all, itself knows nobody     → global.md
Verstack.Layers.Gateway  ← Status / Login / Configuration          → gateway.md
Verstack.Layers.Realm    ← Play phase (Join, Movement)             → realm.md
```

## ServerFeatureLayer

Abstract class from `Verstack.Engine.Lifecycle` — the layer entry point. Through its methods the `ServerComposer`
builds the world (see [engine/index.md](../engine/index.md#serverfeaturelayer)). What the layer author implements:

| Member | Purpose |
|--------|---------|
| `Scope` | The layer's world name (`GLOBAL`/`GATEWAY`/`REALM`) |
| `GetCacheStores()` | Layer aspects — `ProtoAspectInject[]` (component pools, filters) |
| `GetVisibleScopes(...)` | Foreign worlds the layer wants to see (Gateway and Realm request only `GLOBAL`) |
| `GetNextScope()` | Next layer's scope for handoff (Gateway — `REALM`; Global and Realm — empty) |
| `GetHandoffPolicy()` | `BridgeHandoffPolicy` for transfer (Gateway has one; Global and Realm — `null`) |
| `Init(systems)` | Game system registration (after world and visibility are assembled) |

Concrete implementations — [`GlobalLayer`](global.md), [`GatewayLayer`](gateway.md), [`RealmLayer`](realm.md).

## Bundle conveyor

Each Minecraft phase is a set of `PacketBundle`s run through a conveyor. A bundle describes outbound packets via
`PacketOutbound` (see [engine/network.md](../engine/network.md#readers-and-writers)); framing and compression are the
transport's concern. Bundles and conveyors live in `Engine.Network.Packet.Pipeline` (the base mechanism); phase bundles
live inside each layer.

**Two conveyors for different semantics:**

| Conveyor | State | Purpose |
|----------|-------|---------|
| `SequentialPacketPipeline` | stateful (`PacketFlowState`) | Strict step sequence: Handshake→Login→Configuration, Join. Moves strictly forward |
| `DispatchPacketPipeline` | stateless | Arbitrary order: Play packets (movement) are routed by ID in O(1) |

### PacketBundle

Abstract class. Each bundle is a scenario of `StepCount` steps. `TryProcess(stepIndex, entity, in packet, ref outbound)`
handles the packet at the current step and returns `PacketHandleResult`:

| Result | What the conveyor does |
|--------|------------------------|
| `Accepted` | Step passed. `StepIndex++`; on exhausting `StepCount` — `BundleIndex++` (Sequential only) |
| `Ignored` | Packet is legitimate but not the current step's trigger (e.g. `minecraft:brand` in Configuration). Swallowed without advancing |
| `Continue` | Re-check the same packet on the next step (loop inside `ProcessSession`) |
| `Kick` | Packet invalid — client disconnects |

### PacketFlowState

`struct(BundleIndex, StepIndex)` — position in the linear conveyor. Stored in the layer's cache store, one per player
entity. Initialized when the player is put on the "rails" (e.g. in `GuestScreeningSystem` — with Status or Login
`bundleIndex`).

## Intake: from socket to bundle

The player enters a layer through Bridge. Lifecycle on a layer tick (after the Bridge systems
Transfer→Cleanup→Intake→Disconnect):

1. **Intake.** The game system drains new players via `TryDequeueHandoff` (the entity is already `Connected`).
2. **Session creation.** The first packet is parsed manually (Handshake — in `GatewayIntakeHandler`); from it a
   `NetworkSession` is created and `PacketFlowState` is initialized with the right `BundleIndex`.
3. **Dispatch.** Over `ConnectedFilter` runs the main system: `pipeline.ProcessSession(...)` for each active entity.
4. **Handoff.** When the Sequential conveyor reaches the end of the bundle array, it returns
   `PipelineSessionStatus.Transfer`. The layer itself doesn't move the player — `BridgeHandoffPolicy` does, on the next
   tick (in `BridgeTransferSystem`).
5. **Kick.** `PipelineSessionStatus.Kick` → `channel.Disconnect()`. The network will tell the router,
   `BridgeDisconnectSystem` will add `BridgeClientDisconnected`, `BridgeCleanupSystem` will tear down the entity.

## CacheStore

Each layer keeps its component pools and filters in a `ProtoAspectInject` (descendant) — `GatewayCacheStore`,
`UserSessionCacheStore` (Realm), `ServerInfoCacheStore`/`SyncedRegistryCatalog` (Global). Pool access is via `[DI]`;
injection is done by `AutoInjectModule`. Filters are `ProtoIt`/`ProtoItExc`; systems run over them.

A player in a layer is identified by the "entity + `NetworkChannel`" pair. The entity↔channel mapping is held by
`BridgeStateCacheStore` (`GetChannel(entity)`); phase data (session, profile, flowState) lives in the layer's cache store
under the same entity.
