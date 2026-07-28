# The Gateway layer

Gateway is the server's entry layer, the GATEWAY world in ECS. It handles everything that happens before a player enters the game world: Handshake (routing into phases), Status (server-list ping, MOTD), Login, Configuration. It is fully built on ECS: each phase is systems over the world, and packets flow through a conveyor of `PacketBundle`s.

The world is assembled by `GatewayFeature : VerstackFeature` — it registers the aspect (`GatewayCacheStore`), the systems (`GuestScreeningSystem`, `PacketDispatchSystem`), and the `GatewayPacketPipeline` service, then initializes the pipeline. The Feature is plugged into `ServerComposer` alongside `GlobalFeature` and `RealmFeature`. The GATEWAY world sees GLOBAL (see [Architecture](../architecture.md) — world visibility direction).

## Aspect and side data

**`GatewayCacheStore : ProtoAspectInject`** — the world aspect. It holds three component pools:

- `ProtoPool<NetworkSession> Sessions` — a player session: protocolVersion, IP, serverAddress, serverPort (struct).
- `ProtoPool<PacketFlowState> FlowStates` — where the entity is in the bundle conveyor (`BundleIndex`/`StepIndex`).
- `ProtoPool<UserProfile> UserProfiles` — the player profile filled during Login: `Uuid` + `Username`. Stored for the phases that follow (Configuration/Play).

Besides pools, the aspect holds side data: two `entity ↔ NetworkChannel` dictionaries. The forward one (`int → NetworkChannel`) is what systems use to fetch the channel for an entity. The reverse one (`NetworkChannel → int`) is only used to handle disconnects: given a dead channel, find the entity and remove it from the world. `NetworkChannel` is a sealed class with a `PipeReader`/`PipeWriter` — it doesn't fit into a `struct` component, so the link is kept in the aspect rather than in a pool.

## Systems

**`GuestScreeningSystem : IProtoRunSystem`** — guests and the Handshake decision. In `Run()`:

1. Drains `DisconnectedChannels` from `TcpNetworkService`, removes dead channels from its own lists, and if the channel was already in ECS (Status or Login), deletes the entity via `RemoveChannel` + `_world.DelEntity`.
2. Drains `PendingConnections` into `_awaitingHandshake` (an internal list).
3. For each awaiting channel, parses Handshake via `GatewayIntakeHandler.TryParseHandshake`. The result routes: `-1` — kick, `1` (Status) — `PromoteToSession(..., bundleIndex: 0)`, `2` (Login) — `PromoteToSession(..., bundleIndex: 2)`. Both create an ECS entity; they differ only in where the conveyor starts.

`PromoteToSession` creates the entity: `Sessions.NewEntity` returns a `ref` to the slot, where a `NetworkSession` is written with the handshake data, a `PacketFlowState` is added at the given `BundleIndex`, and the link is registered in `GatewayCacheStore`. From here on the channel is driven by `PacketDispatchSystem`.

**`PacketDispatchSystem : IProtoRunSystem`** — the bundle phase. For each entity in `Sessions` it fetches the channel and `FlowState`, rents two `ArrayPool` buffers for the tick, and builds a `PacketOutbound`. It drains `IncomingPackets` and runs each through `GatewayPacketPipeline.TryProcessPacket`. The bundle's response accumulates in the `PacketOutbound` framing buffer and is flushed to the channel in one chunk after the queue drains. If a bundle returns `false`, or if `BundleIndex` has run past the end of the conveyor (all phases passed), the channel is disconnected — first flush, then disconnect, so the send worker always writes into a live `PipeWriter`.

## The bundle conveyor

**`GatewayPacketPipeline : IProtoInitService`** — a service wrapping `PacketPipeline`, injected with `[DI]` into `PacketDispatchSystem`. `Init` builds the ordered bundle array; `TryProcessPacket(entity, packet, ref outbound, ref state)` delegates to the current bundle by `state.BundleIndex`. `BundleCount` exposes the array length so the dispatcher can detect a finished conveyor.

The conveyor, with Status and Login both entity-backed (differing only in the starting `BundleIndex`):

| Index | Bundle | Inbound (step) | Response | Next |
|---|---|---|---|---|
| 0 | `StatusExchangeBundle` | Status Request (0x00) | Status Response (JSON from `ServerInfoCacheStore`) | 1 |
| 1 | `PingPongBundle` | Ping Request (0x01) | Pong Response (echo the long timestamp) | 2 |
| 2 | `LoginStartBundle` | Login Start (0x00) | Set Compression (0x03) + Login Success (0x02) | 3 |
| 3 | `LoginAcknowledgedBundle` | Login Acknowledged (0x03) | — | past the end → disconnect |

Status starts at 0, Login starts at 2 — both run through `PacketDispatchSystem` once promoted. Each bundle is stateless; per-connection state lives in the ECS components on the entity (`NetworkSession`, `PacketFlowState`, `UserProfile`), and the bundle reads/writes them via the `ProtoEntity` it receives.

### Login offline flow

`LoginStartBundle` reads `Name` and the client's `Player UUID` (the latter is ignored — offline mode generates its own). It computes `Uuid.GenerateOfflinePlayer(name)`, writes a `UserProfile` onto the entity via `GetOrAdd`, then sends `Set Compression` (uncompressed — compression is not yet enabled on the channel) followed by `EnableCompression(threshold)`, then `Login Success` (already in compressed framing). `Login Success` carries, per protocol 776: the player `UUID`, the `Username`, an empty `Properties` array, and the `Session ID` UUID (a fresh `Guid.NewGuid()`).

`LoginAcknowledgedBundle` confirms receipt of Login Success. After it, `BundleIndex` runs past the conveyor and `PacketDispatchSystem` closes the channel — Configuration/Play are not implemented yet.

## Handler

`GatewayIntakeHandler` — a stateless helper for `GuestScreeningSystem`. `TryParseHandshake(packet, out (protocolVersion, serverAddress, serverPort) data)` parses the Handshake packet (0x00): protocolVersion, serverAddress, serverPort, nextState. Returns `1` (Status), `2` (Login), or `-1` (invalid). On `1`/`2` it returns the parsed fields via the `out` tuple; `PromoteToSession` writes them into `NetworkSession`.

## Current limitations

- Configuration is not implemented: the layer does not yet handle packets after Login Acknowledged, so the channel is closed at phase completion.
- The `ArrayPool` scratch sizes in `PacketDispatchSystem` (`FRAME_SCRATCH_SIZE = 16 KB`, `PAYLOAD_BUFFER_SIZE = 4 KB`) cover Status/Login with headroom but are too small for Play-phase chunks — a dynamic size or per-packet flush will be needed there.
