# The Gateway layer

Gateway is the server's entry layer, the GATEWAY world in ECS. It handles everything that happens before a player enters the game world: Handshake (routing into phases), Status (server-list ping, MOTD), Login, Configuration. It is fully built on ECS: each phase is systems over the world, and packets flow through a conveyor of `PacketBundle`s.

The world is assembled by `GatewayFeature : VerstackFeature` — it registers the aspect (`GatewayCacheStore`) and the systems (`GuestScreeningSystem`, `PacketDispatchSystem`), plus the `GatewayPacketPipeline` service. The Feature is plugged into `ServerComposer` alongside `GlobalFeature` and `RealmFeature`. The GATEWAY world sees GLOBAL (see [Architecture](../architecture.md) — world visibility direction).

## Aspect and side data

**`GatewayCacheStore : ProtoAspectInject`** — the world aspect. It holds two component pools:

- `ProtoPool<NetworkSession> Sessions` — a player session: protocolVersion, IP, serverAddress, serverPort (struct).
- `ProtoPool<PacketFlowState> FlowStates` — where the entity is in the bundle conveyor (`BundleIndex`/`StepIndex`).

Besides pools, the aspect holds side data: two `entity ↔ NetworkChannel` dictionaries. The forward one (`int → NetworkChannel`) is what systems use to fetch the channel for an entity to write the response. The reverse one (`NetworkChannel → int`) is only used to handle disconnects: given a dead channel, find the entity and remove it from the world. `NetworkChannel` is a sealed class with a `PipeReader`/`PipeWriter` — it doesn't fit into a `struct` component, so the link is kept in the aspect rather than in a pool.

## Systems

**`GuestScreeningSystem : IProtoInitSystem, IProtoRunSystem`** — guests and Status. In `Run()`:

1. Drains `DisconnectedChannels` from `TcpNetworkService`, removes dead channels from its own lists, and if the channel was already in ECS (Login), deletes the entity via `RemoveChannel` + `_world.DelEntity`.
2. Drains `PendingConnections` into `_awaitingHandshake` (an internal list).
3. For each awaiting channel, parses Handshake via `GatewayIntakeHandler.TryParseHandshake`. The result routes: `-1` — kick, `1` (Status) — move into `_statusConnections`, `2` (Login) — create an ECS entity: `Sessions.NewEntity` returns a `ref` to the slot, where a `NetworkSession` is written with data from the handshake plus the channel's IP, a `PacketFlowState` is added (starting at `BundleIndex = 0`), and the link is registered in `GatewayCacheStore`.
4. For Status channels it serves ping/MOTD directly via `GatewayIntakeHandler.TryHandleStatusRequest` — without creating an ECS entity (Status is a short, sessionless phase).

**`PacketDispatchSystem : IProtoRunSystem`** — the bundle phase. It iterates all entities in `Sessions`, fetches each entity's channel and `FlowState`, drains `IncomingPackets`, and runs each packet through `GatewayPacketPipeline.TryProcessPacket`. If a bundle returns `false`, the packet is invalid and the channel is disconnected. After each packet, the response is flushed to the socket.

## The bundle conveyor

**`GatewayPacketPipeline`** — a service wrapping `PacketPipeline`, injected with `[DI]` into `PacketDispatchSystem`. It holds an array of `PacketBundle` (empty for now — bundles are in progress). `TryProcessPacket(packet, writer, ref state)` delegates to the current bundle by `state.BundleIndex`. A bundle may advance `state.BundleIndex` to transition to the next phase (Login → Configuration).

The concept: each Minecraft phase is a separate `PacketBundle` with its own packets and transition logic. Status is served separately (in `GuestScreeningSystem`, no entity), while Login/Configuration go through the conveyor, one entity per player.

## Handler

`GatewayIntakeHandler` — a stateless helper for `GuestScreeningSystem`. `TryParseHandshake(packet, out HandshakeData)` parses the Handshake packet (0x00): protocolVersion, serverAddress, serverPort, nextState. Returns `1`/`2`/`-1`. `TryHandleStatusRequest(packet, writer)` handles Status Request (0x00, returns JSON from `ServerInfoCacheStore`) and Ping (0x01, echoes the long). It pulls `ServerInfoCacheStore` from the GLOBAL world — that is the point where Gateway sees Global.

## Current limitations

- The Login/Configuration bundles are not written: `GatewayPacketPipeline` is initialized with an empty array. Any packet from a logged-in player currently leads to a kick — `TryProcessPacket` returns `false` on a `BundleIndex` outside the empty array.
- The send side is synchronous: `channel.Writer.FlushAsync().GetAwaiter().GetResult()` in `GuestScreeningSystem` and `PacketDispatchSystem`. One slow writer stalls, and the whole Gateway tick stalls with it — counter to the backpressure idea. A move to a send queue with a worker is planned.
