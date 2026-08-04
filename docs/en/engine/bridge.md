# Bridge

The decoupling of the async network layer and the synchronous ECS tick. This is the only path by which a player's
connection moves from one layer to another: Gateway and Realm [do not see each other](../architecture.md#worlds-and-visibility),
and Bridge transfers channel ownership between them.

Bridge sits between `Engine.Network` and the phase layers. The network pumps bytes in background threads; ECS runs in a
single tick-loop thread and is not thread-safe. Bridge mates these two worlds through queues and the player state machine.

## Composition

All types are in the `Verstack.Engine.Bridge` project. The four systems are `internal sealed`; the rest is public
contract for the layers.

**Router and module (one instance per server):**

| Type | Role |
|-----|------|
| `BridgeHandoffRouter` | Central hub. Owns channel ownership, decouples threads and ECS via `ConcurrentQueue`. Implements `ClientLifecycleHandler` (see [engine/index.md](index.md#network--bridge-junction)) |
| `BridgeLayerModule` | Bridge integration into a specific ECS layer. `IProtoModule`: installs four systems in fixed order and registers the `BridgeStateCacheStore` aspect. **Not injected into Global** |

**Aspect (instance per layer):**

| Type | Role |
|-----|------|
| `BridgeStateCacheStore` | `ProtoAspectInject` (CacheStore). Player state machine, filters, component pools, entity↔channel mappings, FIFO handoff queue. Operates only over its own world's entities |

**Components (ECS state markers):**

| Component | Meaning |
|-----------|---------|
| `BridgeHandoffPending` | Player created in ECS, but awaits initialization by the layer's specific systems |
| `BridgeClientConnected` | Player on the "rails" — active, visible to game systems |
| `BridgeClientDisconnected` | Connection lost; awaiting cleanup |

**Data contract between layers:**

| Type | Role |
|-----|------|
| `BridgeHandoffData` | Abstract `record` marker for DTOs. Descendants (records) live in shared contracts and carry data the receiving layer needs to initialize the player |
| `HandoffPayload` | `readonly struct`, returned from `TryDequeueHandoff`: entity + DTO |
| `BridgeHandoffPolicy` | Abstract transfer policy. Implemented by a layer that hands players off; decides whether the player is ready to move |

## Player state machine

A player's entity in Bridge passes states via three markers:

```text
Pending → Connected → (Disconnected | Handoff)
   │         │              │
   │         │              └─ BridgeTransferSystem: leaves for the next layer (socket stays alive)
   │         │
   │         └─ a layer's game system: TryDequeueHandoff
   │
   └─ BridgeIntakeSystem: created from the router queue
```

- **Pending.** Entity created by Bridge, awaits a game system to claim it from the queue. Game systems **do not see it**
  — it has no `BridgeClientConnected`.
- **Connected.** A game system claimed the player (`TryDequeueHandoff`), `BridgeHandoffPending` removed,
  `BridgeClientConnected` added. Now the entity is visible in `ConnectedFilter` — the layer's phase logic runs on it.
- **Disconnected.** The TCP thread reported a disconnect; `BridgeDisconnectSystem` added the marker. The entity lives
  until the next `BridgeCleanupSystem`.

A layer that doesn't hand players off (Realm) has no `BridgeHandoffPolicy` — its players live in Connected until disconnect.

## Tick pipeline

`BridgeLayerModule.Init` registers four systems in strict order. On each layer tick they run sequentially:

| # | System | What it does |
|---|--------|--------------|
| 1 | `BridgeTransferSystem` | For each `ConnectedFilter`: if `handoffPolicy.TryTransfer` approved — `router.TransferToNext` (ownership → next scope, into next's queue), `Remove(e, closeSocket:false)`. Socket **is not closed** — it passed to the next layer's ownership. Dead-end layer (`nextScope` empty) — early return |
| 2 | `BridgeCleanupSystem` | Removes garbage (`PendingGarbageFilter`) and disconnected (`DisconnectedFilter`). `Remove(e, closeSocket:true)` — socket **is closed** |
| 3 | `BridgeIntakeSystem` | `router.TryDequeuePending` → `RegisterPending(channel, data)`. Race guard: if the channel is already `IsDisconnected`, skips it |
| 4 | `BridgeDisconnectSystem` | `router.GetDisconnected(scope)` → `MarkDisconnected` (adds `BridgeClientDisconnected`) |

The order isn't accidental: Transfer leaves **before** game systems, so handed-off players don't run an extra tick in the
departing layer; Cleanup runs **before** Intake, so newly arrived in the current tick don't mix with those being removed;
Disconnect sets markers **at the end** of the tick — they fire in the next tick's Cleanup.

After the Bridge systems come the layer's game systems (`GatewayIntakeHandler`, `PacketDispatchSystem`, etc.) — they work
with `ConnectedFilter` and `TryDequeueHandoff`.

## Thread decoupling

`BridgeHandoffRouter` is the only object both threads touch. The TCP thread (`TcpNetworkService`) writes; the ECS tick
reads:

- `HandleConnect(channel)` (TCP thread): `ownership[channel] = defaultScope` (usually GATEWAY), `pending[defaultScope].Enqueue`.
- `HandleDisconnect(channel)` (TCP thread): looks up the current owner, `disconnected[owner].Enqueue`.
- `TransferToNext(currentScope, channel, data)` (ECS tick, from the Transfer system): under a `Lock`, verifies the
  current owner is `currentScope`, moves ownership to `nextScope`, `pending[nextScope].Enqueue`.

Access to `_ownership` is guarded by a `Lock`; the `ConcurrentQueue<PendingTransfer>` / `ConcurrentQueue<NetworkChannel>`
queues are split by scope (`Dictionary<string, ConcurrentQueue<...>>`), one pair per layer. Thus the TCP thread writes
into a "foreign" scope only on connect/disconnect, and each layer's ECS tick reads only its own queue.

## Contract for the game layer

A layer's game system gets the player like this:

```csharp
[DI] private readonly BridgeStateCacheStore _state = null!;

// in Run():
while (_state.TryDequeueHandoff(out var payload))
{
    // payload.Entity — entity in the Connected state
    // payload.Data   — BridgeHandoffData from the previous layer (or null on first entry)
    // then: attach phase components, start the scenario (login, join, etc.)
}
```

`TryDequeueHandoff` moves the entity from Pending to Connected (adds `BridgeClientConnected`, removes `BridgeHandoffPending`)
and returns `HandoffPayload`. If the player disconnected while sitting in the queue — skipped (will be removed as
garbage). The `ConnectedFilter` is `It.Inc<BridgeClientConnected>().Exc<BridgeClientDisconnected>()`: all phase packet
processing systems run on it.

To hand the player off, the layer implements `BridgeHandoffPolicy.TryTransfer(entity, channel, out data)` — returns
`true` when the internal condition is met (login passed, profile loaded) and packs the DTO `data` for the next layer.
