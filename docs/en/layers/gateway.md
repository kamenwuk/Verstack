# Gateway

The GATEWAY world — the entry layer. Handles Handshake and walks the connection through Status, Login, and Configuration
up to the point of handing the player to [Realm](realm.md). Doesn't see Realm directly — the handoff goes through
[Bridge](../engine/bridge.md).

Implementation — `GatewayLayer : ServerFeatureLayer` (`Scope = GATEWAY`): sees `GLOBAL`, next layer — `REALM`, policy —
`GatewayHandoffPolicy`. Two game systems: `GuestScreeningSystem` (Handshake intake) and `PacketDispatchSystem` (the
bundle conveyor). Cache store — `GatewayCacheStore`.

## Scenario: from socket to Realm

The scenario is linear — a `SequentialPacketPipeline` of 7 bundles. State lives in `PacketFlowState` (in
`GatewayCacheStore`); the position is initialized in `GuestScreeningSystem` after Handshake parsing.

```text
accept → [Bridge: Pending → Connected]
   ↓
GuestScreeningSystem: first packet (Handshake 0x00)
   ├─ nextState=1 → Status   (BundleIndex=0)
   └─ nextState=2 → Login    (BundleIndex=2)
   ↓
PacketDispatchSystem → SequentialPacketPipeline:

  [0] StatusExchangeBundle   Status Request 0x00  → Status Response (JSON from ServerInfo)
  [1] PingPongBundle         Ping 0x01            → Pong (same timestamp)
        ── the Status scenario ends, the client leaves. ──
  [2] LoginStartBundle       Login Start 0x00     → Set Compression + Login Success
  [3] LoginAcknowledgedBundle Login Ack 0x03      → (transition to Configuration)
  [4] ClientInformationBundle Client Info 0x00    → Known Packs (minecraft:core@26.2)
  [5] KnownPacksBundle       Known Packs 0x07     → Registry Data + Update Tags + Feature Flags + Finish
  [6] ConfigurationFinishBundle Ack 0x03          → (placeholder)
        ── end of conveyor → Transfer ──
   ↓
GatewayHandoffPolicy.TryTransfer: BundleIndex ≥ 6 → EnterRealmHandoffData → Bridge → Realm
```

`PacketDispatchSystem.Run` runs `ProcessSession` over `ConnectedFilter`. `PipelineSessionStatus.Transfer` (end of the
bundle array) — the layer does nothing; `GatewayHandoffPolicy` does the work in the next tick's `BridgeTransferSystem`.
`Kick` — `channel.Disconnect()`, then Bridge tears down the entity.

## Bundles

| Bundle | Steps | Client packet | What it does |
|--------|-------|---------------|--------------|
| `StatusExchangeBundle` | 1 | Status Request `0x00` | Sends Status Response with JSON from `ServerInfoCacheStore` (Global) |
| `PingPongBundle` | 1 | Ping `0x01` | Echoes the timestamp back |
| `LoginStartBundle` | 1 | Login Start `0x00` | Reads the name, generates an offline UUID (MD5, version 3), stores `UserProfile`, sends `Set Compression` + `Login Success` |
| `LoginAcknowledgedBundle` | 1 | Login Ack `0x03` | Logs, transitions to Configuration |
| `ClientInformationBundle` | 1 | Client Info `0x00` | Reads the locale into `UserProfile`, sends Known Packs (`minecraft:core@26.2`). `minecraft:brand` (`0x02`) — `Ignored` |
| `KnownPacksBundle` | 3 | Known Packs `0x07` | Step 0: Registry Data (`0x07`) over the `SyncedRegistryCatalog`. Step 1: Update Tags (`0x0D`) from assets. Step 2: Feature Flags (`0x0C`) + Finish Configuration (`0x03`). Uses `Continue` between steps |
| `ConfigurationFinishBundle` | 1 | Ack `0x03` | Current implementation — placeholder, returns `Kick` |

### Subtle point: Set Compression

`LoginStartBundle` sends `Set Compression` (`0x03`) **before** calling `EnableCompression`. This isn't a bug: Set
Compression must go out uncompressed (compression isn't enabled on the channel yet), and the next packet — Login
Success — is already in compressed framing. `PacketOutbound.Commit` reads the channel's threshold live, so the mode
switch applies right after Set Compression. Encryption and Mojang authentication (online mode) are outside the
offline MVP.

## GatewayHandoffPolicy

The transfer policy to Realm. `TryTransfer` returns `true` when:

1. The entity has `UserProfile` and `NetworkSession` (login passed).
2. `FlowState.BundleIndex ≥ 6` — the conveyor reached the end of Configuration.

It packs `EnterRealmHandoffData(profile, session)` and hands it to Bridge. `BridgeTransferSystem` moves channel
ownership into Realm; the socket isn't closed.

## GatewayCacheStore

`ProtoAspectInject`: pools `NetworkSession`, `UserProfile`, `PacketFlowState` and the `ActiveSessionsFilter`
(`Inc<NetworkSession, PacketFlowState>`). Phase data lives under the same entity as in Bridge — the link is provided by
`BridgeStateCacheStore.GetChannel(entity)`.

Access to the foreign Global world — via `systems.NamedWorlds()[ServerWorldScopes.GLOBAL].Aspect<T>()` in the bundle's
`Init` (e.g. `StatusExchangeBundle` takes `ServerInfoCacheStore`, `KnownPacksBundle` — `SyncedRegistryCatalog`).
