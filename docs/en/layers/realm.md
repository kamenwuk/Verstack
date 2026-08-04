# Realm

The REALM world — the Play phase. Takes a player from [Gateway](gateway.md) via [Bridge](../engine/bridge.md), runs the
game entry scenario (Join), and from then on processes the player's input (Movement). A dead-end layer: doesn't hand the
player off (`GetNextScope` empty, `GetHandoffPolicy` — `null`); the player lives here until disconnect.

Implementation — `RealmLayer : ServerFeatureLayer` (`Scope = REALM`): sees `GLOBAL`, two game systems —
`HandoffApprovalSystem` (handoff intake + Join conveyor) and `InboundDispatcherSystem` (input). Cache store —
`UserSessionCacheStore`.

## Handoff intake and Join

`HandoffApprovalSystem` works in two phases on each tick:

**Phase 1 — handoff approval.** Drains new players via `_bridgeStateCacheStore.TryDequeueHandoff`. If
`payload.Data` is `EnterRealmHandoffData`, seeds into `UserSessionCacheStore` under the entity: `UserProfile`,
`NetworkSession`, `PacketFlowState(0, 0)`. After this the entity is ready for the Join scenario.

**Phase 2 — Join conveyor.** Over `ConnectedFilter` runs a `SequentialPacketPipeline` of 6 bundles. State —
`PacketFlowState` (in `UserSessionCacheStore`).

```text
[0] JoinLoginBundle         waits for Login Ack 0x03  → Login (Play) 0x31 (game mode, dimension, seed, sea level)
[1] JoinSpawnPointBundle    (no trigger)              → set_default_spawn_position 0x61
[2] JoinTabListBundle       (no trigger)              → tab list
[3] JoinCommandCatalogBundle (no trigger)             → command catalog
[4] JoinChunkBatchBundle    (no trigger)              → chunk batch (client readiness for chunks)
[5] JoinTeleportBundle      (no trigger)              → teleport the player to spawn
```

The first bundle (`JoinLoginBundle`) is the only one waiting for a client packet: `Login Acknowledged 0x03`. Other
packets on this step are `Ignored`. The remaining 5 bundles are triggered by conveyor movement (`Continue`), not by an
incoming packet — the server itself sends commands to the client. `PipelineSessionStatus.Transfer` (end of the array)
means Join complete — the player is fully in the game.

## Player input

After Join, input is handled by `InboundDispatcherSystem` + `DispatchPacketPipeline` (stateless, routing by packet ID):

| Packet ID | Bundle | Purpose |
|-----------|--------|---------|
| `0x00` | `ConfirmTeleportBundle` | Teleport confirmation (clientbound teleport → ack) |
| `0x1E` | `SetPlayerPositionBundle` | Movement: position without rotation |
| `0x1F` | `SetPlayerPositionAndRotationBundle` | Movement: position + rotation |

Play packets arrive in arbitrary order — hence Dispatch here, not Sequential. `Kick` → `channel.Disconnect()`, then
Bridge tears down the entity.

## UserSessionCacheStore

`ProtoAspectInject`: pools `NetworkSession`, `UserProfile`, `PacketFlowState`. The same types as in `GatewayCacheStore`,
but the instances are its own, under the same entity (it moves between layers via Bridge). Join bundle phase data is
taken from `SpawnConstants`/`WorldConstants` (Global); the constants are static, no injection.

## What's next

The current scope is Join + basic movement. World simulation (chunks, entities, game logic) is not implemented and will
be built on top of this same structure: systems over `ConnectedFilter`, input handling via the Dispatch pipeline, state
sync via outbound packets.
