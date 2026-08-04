# Global

The GLOBAL world — the shared root, visible to all layers, itself knows nobody. It doesn't work with sockets or phase
packets; its role is to hold what the others need: server info, synced registries, assets, world/spawn constants, shared
player models.

Implementation — `GlobalLayer : ServerFeatureLayer` (`Scope = GLOBAL`). Doesn't request foreign worlds
(`GetVisibleScopes` empty), hands players nowhere (`GetNextScope` empty, `GetHandoffPolicy` — `null`). Its only system is
`UpdateServerInfoSystem`.

## ServerInfoCacheStore

Server status cache for the ping response (Status phase): MOTD, player cap, version/protocol, online counter. Holds the
ready response JSON (`_cachedStatusJson`) with a dirty-flag: on online change (`SetOnlinePlayers`) the flag is set, but
the JSON isn't rebuilt right there — `UpdateServerInfoSystem` does it once per second
(`SERVER_INFO_UPDATE_INTERVAL`). The lazy `GetStatusJson` rebuilds the JSON if a ping arrives before the first tick.

Set by `GlobalLayer` constructor parameters: MOTD, max players, `"26.2"` (version), `776` (protocol).

## SyncedRegistryCatalog

Static catalog of Minecraft 26.2 synced registries (29 items). Used in the Configuration phase — `KnownPacksBundle`
sends the client Registry Data for each registry. Contains:

- `RegistryIds` — byte identifiers of registries (`"minecraft:dimension_type"`, etc.).
- `MandatoryEntries` — mandatory entries for each registry (`"minecraft:overworld"` for `dimension_type`, etc.).
  For optional registries the array is empty.
- `RegistryType` (`enum : byte`) — type-safe index (29 values), order **strictly matches** the indices of both arrays.
- `GetId`/`GetEntries` — access shims by enum.

The catalog is the single source of truth on which registries and entries the server obligates itself to sync. The
actual registry data (entry content) is loaded from assets via `Verstack.Shared.Assets`.

## Assets — owner

Global is the only layer through which the others get access to compiled assets. Loading goes through
`Verstack.Shared.Assets` (`AssetCatalog` and cache buffers); the data compilation pipeline is described in
[shared/index.md](../shared/index.md). Layers read registry/tag data only through this path, not directly.

## World and spawn constants

`SpawnConstants` and `WorldConstants` — static parameters of the Play phase:

- `SpawnConstants`: the spawn dimension's type/name (`minecraft:overworld`, id 0), spawn block coordinates (for the
  compass and entry teleport), yaw/pitch. The coordinates are derived so the player's feet stand on the FlatGenerator
  surface without falling through.
- `WorldConstants`: view/simulation distances, game mode on entry (creative by default).

Used in the Join bundles (Realm) — `JoinSpawnPointBundle`, `JoinLoginBundle`.

## Player models

`Layers.Global.User` holds shared `readonly struct` models not tied to a specific layer:

- `NetworkSession` — connection parameters: protocol version, IP, server address/port from Handshake.
- `UserProfile` — player identity: UUID, name, locale.

These types move between layers via `EnterRealmHandoffData` (a `BridgeHandoffData` descendant in Global): Gateway
collects them on login, passes them to Realm via [Bridge](../engine/bridge.md), Realm seeds them into its
`UserSessionCacheStore`. They live in Global because both layers see the type definitions, but each holds its own
instances in its own cache store.
