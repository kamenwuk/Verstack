# The Gateway layer

Gateway is the server's entry layer, the GATEWAY world in ECS. It handles everything that happens before a player enters the game world: Handshake (routing into phases), Status (server-list ping, MOTD), Login, Configuration. It is fully built on ECS: each phase is systems over the world, and packets flow through a conveyor of `PacketBundle`s.

The world is assembled by `GatewayFeature : VerstackFeature` — it registers the aspect (`GatewayCacheStore`), the systems (`GuestScreeningSystem`, `PacketDispatchSystem`), and the `GatewayPacketPipeline` service, then initializes the pipeline. The Feature is plugged into `ServerComposer` alongside `GlobalFeature` and `RealmFeature`. The GATEWAY world sees GLOBAL (see [Architecture](../architecture.md) — world visibility direction).

## Aspect and side data

**`GatewayCacheStore : ProtoAspectInject`** — the world aspect. It holds three component pools:

- `ProtoPool<NetworkSession> Sessions` — a player session: protocolVersion, IP, serverAddress, serverPort (struct).
- `ProtoPool<PacketFlowState> FlowStates` — where the entity is in the bundle conveyor (`BundleIndex`/`StepIndex`).
- `ProtoPool<UserProfile> UserProfiles` — the player profile, filled in stages: `Uuid` + `Username` during Login, `Locale` during Configuration (from Client Information). Stored for the phases that follow (Play).

Besides pools, the aspect holds side data: two `entity ↔ NetworkChannel` dictionaries. The forward one (`int → NetworkChannel`) is what systems use to fetch the channel for an entity. The reverse one (`NetworkChannel → int`) is only used to handle disconnects: given a dead channel, find the entity and remove it from the world. `NetworkChannel` is a sealed class with a `PipeReader`/`PipeWriter` — it doesn't fit into a `struct` component, so the link is kept in the aspect rather than in a pool.

## Systems

**`GuestScreeningSystem : IProtoRunSystem`** — guests and the Handshake decision. In `Run()`:

1. Drains `DisconnectedChannels` from `TcpNetworkService`, removes dead channels from its own lists, and if the channel was already in ECS (Status or Login), deletes the entity via `RemoveChannel` + `_world.DelEntity`.
2. Drains `PendingConnections` into `_awaitingHandshake` (an internal list).
3. For each awaiting channel, parses Handshake via `GatewayIntakeHandler.TryParseHandshake`. The result routes: `-1` — kick, `1` (Status) — `PromoteToSession(..., bundleIndex: 0)`, `2` (Login) — `PromoteToSession(..., bundleIndex: 2)`. Both create an ECS entity; they differ only in where the conveyor starts.

`PromoteToSession` creates the entity: `Sessions.NewEntity` returns a `ref` to the slot, where a `NetworkSession` is written with the handshake data, a `PacketFlowState` is added at the given `BundleIndex`, and the link is registered in `GatewayCacheStore`. From here on the channel is driven by `PacketDispatchSystem`.

**`PacketDispatchSystem : IProtoRunSystem`** — the bundle phase. For each entity in `Sessions` it fetches the channel and `FlowState`, rents two `ArrayPool` buffers for the tick, and builds a `PacketOutbound`. It drains `IncomingPackets` and runs each through `GatewayPacketPipeline.TryProcessPacket`. The bundle's response accumulates in the `PacketOutbound` framing buffer and is flushed to the channel in one chunk after the queue drains. If a bundle returns a kick (`PacketHandleResult.Kick`), or if `BundleIndex` has run past the end of the conveyor (all phases passed), the channel is disconnected — first flush, then disconnect, so the send worker always writes into a live `PipeWriter`. Packets returned as `Ignored` are swallowed by the conveyor without advancement and without a kick.

## The bundle conveyor

**`GatewayPacketPipeline : IProtoInitService`** — a service wrapping `PacketPipeline`, injected with `[DI]` into `PacketDispatchSystem`. `Init` builds the ordered bundle array; `TryProcessPacket(entity, packet, ref outbound, ref state)` delegates to the current bundle by `state.BundleIndex`. `BundleCount` exposes the array length so the dispatcher can detect a finished conveyor.

Each bundle returns a `PacketHandleResult`: `Accepted` (step passed, the conveyor advances `StepIndex`/`BundleIndex`), `Ignored` (the packet is swallowed without advancement — for packets that are foreign but legitimate in the phase, e.g. `minecraft:brand` in Configuration), or `Kick` (invalid packet, disconnect). The conveyor's `TryProcessPacket` exposes a `bool` to the outside: `true` means continue (`Accepted`/`Ignored`), `false` means kick.

The conveyor, with Status and Login both entity-backed (differing only in the starting `BundleIndex`):

| Index | Bundle | Inbound (step) | Response | Next |
|---|---|---|---|---|
| 0 | `StatusExchangeBundle` | Status Request (0x00) | Status Response (JSON from `ServerInfoCacheStore`) | 1 |
| 1 | `PingPongBundle` | Ping Request (0x01) | Pong Response (echo the long timestamp) | 2 |
| 2 | `LoginStartBundle` | Login Start (0x00) | Set Compression (0x03) + Login Success (0x02) | 3 |
| 3 | `LoginAcknowledgedBundle` | Login Acknowledged (0x03) | — | 4 |
| 4 | `ClientInformationBundle` | Client Information (0x00) | Known Packs (0x0E): `minecraft:core@26.2` | 5 |
| 5 | `KnownPacksBundle` | Known Packs response (0x07) | Feature Flags (0x0C) + Finish Configuration (0x03) | 6 |
| 6 | `ConfigurationFinishBundle` | Acknowledge Finish (0x03) | Disconnect (0x02, JSON reason) | past the end → disconnect |

Status starts at 0, Login starts at 2 — Configuration continues from 4 after Login Acknowledged. All run through `PacketDispatchSystem` once promoted. Each bundle is stateless; per-connection state lives in the ECS components on the entity (`NetworkSession`, `PacketFlowState`, `UserProfile`), and the bundle reads/writes them via the `ProtoEntity` it receives.

### Login offline flow

`LoginStartBundle` reads `Name` and the client's `Player UUID` (the latter is ignored — offline mode generates its own). It computes `Uuid.GenerateOfflinePlayer(name)`, writes a `UserProfile` onto the entity via `GetOrAdd`, then sends `Set Compression` (uncompressed — compression is not yet enabled on the channel) followed by `EnableCompression(threshold)`, then `Login Success` (already in compressed framing). `Login Success` carries, per protocol 776: the player `UUID`, the `Username`, an empty `Properties` array, and the `Session ID` UUID (a fresh `Guid.NewGuid()`).

`LoginAcknowledgedBundle` confirms receipt of Login Success. After it the client enters the Configuration state, and the conveyor continues with `ClientInformationBundle` — the channel is no longer closed at this step.

### Configuration flow

Configuration is the phase after Login Acknowledged that brings the client to Play readiness. It is implemented as three reactive bundles (same as Status/Login: wait for a client trigger packet, then respond):

- `ClientInformationBundle` (0x00 → 0x0E). Reads `locale` from Client Information and stores it in `UserProfile` (will be needed in Play). Sends S→C Known Packs with one pack, `minecraft:core@26.2` — the server blocks Configuration until it receives the client's response.
- `KnownPacksBundle` (0x07 → 0x0C + 0x03). Reads the subset of packs known to the client, then sends Feature Flags (`["minecraft:vanilla"]`) and Finish Configuration.
- `ConfigurationFinishBundle` (0x03 → 0x02). On Acknowledge Finish Configuration, sends a Disconnect with a JSON reason and closes the channel. Play is not implemented yet (REALM is empty).

Packets the client sends proactively during Configuration (e.g. `minecraft:brand`, C→S 0x02) are returned by the bundles as `Ignored` — the conveyor swallows them without a kick and without advancement. Registry Data (S→C 0x07) is not sent yet: registry listing goes listing-only (bodies are omitted), and this is marked with a `TODO` in `KnownPacksBundle`. The writer itself is already implemented ([NBT](../nbt/index.md)) — listing Registry Data with full bodies is a separate task.

## Handler

`GatewayIntakeHandler` — a stateless helper for `GuestScreeningSystem`. `TryParseHandshake(packet, out (protocolVersion, serverAddress, serverPort) data)` parses the Handshake packet (0x00): protocolVersion, serverAddress, serverPort, nextState. Returns `1` (Status), `2` (Login), or `-1` (invalid). On `1`/`2` it returns the parsed fields via the `out` tuple; `PromoteToSession` writes them into `NetworkSession`.

## Current limitations

- The Configuration phase is implemented without Registry Data: registry listing goes listing-only (bodies are omitted). The skeleton brings the client to Disconnect, but a clean vanilla client may not reach Acknowledge Finish Configuration — at that step it validates registry/tag data (the MC-249007 cache helps only on re-entry within the same client process). The `Verstack.NBT` writer is already implemented ([NBT](../nbt/index.md)); a full Registry Data listing is a separate task.
- After Configuration the channel is closed with an informational Disconnect: Play is not implemented (REALM is empty).
- The `ArrayPool` scratch sizes in `PacketDispatchSystem` (`FRAME_SCRATCH_SIZE = 16 KB`, `PAYLOAD_BUFFER_SIZE = 4 KB`) cover Status/Login/Configuration without Registry Data with headroom, but are too small for Play-phase chunks and Registry Data — a dynamic size or per-packet flush will be needed there.
