# The Global layer

Global is the GLOBAL world in ECS, visible to every other world (Gateway, Realm). It holds data and subsystems shared across the whole server: the server-list-ping status (MOTD/version/slots), server time, constants. There is nothing phase- or connection-specific here — only global state.

Assembly is done by `GlobalFeature : VerstackFeature` — it registers the `ServerInfoCacheStore` aspect and the `UpdateServerInfoSystem` system. The Feature is plugged into `ServerComposer`. The GLOBAL world always ticks, even when Gateway/Realm are paused (DDoS backpressure) — see [Architecture](../architecture.md).

## ServerInfoCacheStore

**`ServerInfoCacheStore : ProtoAspectInject`** — the world aspect. It holds the server-status fields: `Motd`, `MaxPlayers`, `VersionName`, `ProtocolVersion`, `OnlinePlayers`. The core idea is zero allocations on a server-list ping: the status JSON is rebuilt only on change and cached as a `byte[] _cachedStatusJson`.

The mechanism is a dirty flag:

- `SetOnlinePlayers(count)` — called by a system when a player joins or leaves. If the value is unchanged, it does nothing. If it changed, it sets `_isDirty = true` without allocating anything.
- `RebuildIfDirty()` — rebuilds the JSON via `JsonSerializer.SerializeToUtf8Bytes` if dirty. Called by the system once a second and in `GetStatusJson()` in case of a ping before the first tick.
- `GetStatusJson()` — returns the ready `byte[]`. Called from Gateway on a Status Request.

The constructor takes the initial values: `new ServerInfoCacheStore("A Minecraft Server", 100, "26.2", 776)`. `26.2`/`776` is the Minecraft 1.21.x version.

## UpdateServerInfoSystem

**`UpdateServerInfoSystem : IProtoInitSystem, IProtoRunSystem`** — once per `ServerConstants.SERVER_INFO_UPDATE_INTERVAL` (1 sec) it calls `RebuildIfDirty()`. It accumulates `_timer` via `_serverTime.DeltaTime` (the injected `ServerTime`); on exceeding the interval it resets and rebuilds. This keeps the cache fresh without allocations on every tick and every ping.

## ServerConstants

A static class with the server's base constants:

- `TICKS_PER_SECOND = 20` — the standard Minecraft TPS.
- `TICK_INTERVAL = 1.0 / 20` — the tick duration in seconds (50 ms). Used in `EntryPoint.RunMainLoop` to compute the sleep.
- `SERVER_INFO_UPDATE_INTERVAL = 1.0` — the status-cache refresh interval.

## ServerTime

**`ServerTime`** — a service, injected with `[DI]` into systems. It tracks time via `Stopwatch.GetTimestamp()` (a high-precision CPU timer):

- `DeltaTime` — the previous tick's duration in seconds.
- `TotalTime` — total server uptime. Computed directly from the start (`currentTimestamp * TickFrequency`), without accumulating DeltaTime — to avoid accumulating error (drift).

`Update()` is called at the end of every tick in `EntryPoint.RunMainLoop`. It is not an ECS component but a plain service class — because time is shared across all worlds and is not tied to an entity.

## Relation to other layers

GLOBAL is visible from Gateway and Realm via the named world `[DI(WorldScopes.GLOBAL)]`. The Gateway → Global contact point is `GatewayIntakeHandler`, which in `Init` fetches `ServerInfoCacheStore` from the GLOBAL world via `world.Aspect<ServerInfoCacheStore>()` and uses its `GetStatusJson()` on a Status Request. Realm will reach Global the same way — once Play-phase systems appear.
