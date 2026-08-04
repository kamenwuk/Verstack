# Слой Global

Global — GLOBAL-мир в ECS, виден всем остальным мирам (Gateway, Realm). Содержит данные и подсистемы, общие для всего сервера: статус для пинга сервер-листа (MOTD/версия/слоты), каталог синхронизированных реестров 26.2, общие типы данных игрока и DTO для передачи между слоями. Здесь нет ничего специфичного для фазы или соединения — только глобальное состояние.

Сборку делает `GlobalLayer : ServerFeatureLayer` (см. [Lifecycle](../architecture.md) про базовый класс) — регистрирует аспект `ServerInfoCacheStore` и систему `UpdateServerInfoSystem`. `GetNextScope()` возвращает пустую строку (Global — корневой слой, в цепочке handoff не участвует), `GetHandoffPolicy()` возвращает `null`. Слой подключается в `EntryPoint.Start` первым аргументом (`globalLayer`), остальные слои — массивом.

## ServerInfoCacheStore

**`ServerInfoCacheStore : ProtoAspectInject`** — аспект мира. Хранит поля статуса сервера: `Motd`, `MaxPlayers`, `VersionName`, `ProtocolVersion`, `OnlinePlayers`. Главная идея — нулевые аллокации на пинге сервер-листа: JSON статуса пересобирается только при изменении и кэшируется в `byte[]`.

Конструктор принимает начальные значения: `new ServerInfoCacheStore("A Minecraft Server", 100, "26.2", 776)`. `26.2`/`776` — версия Minecraft 1.21.x.

Механика через dirty-flag:

- `SetOnlinePlayers(count)` — вызывается системой, когда игрок заходит или выходит. Если значение не изменилось — ничего не делает. Если изменилось — ставит `_isDirty = true`, ничего не аллоцируя.
- `RebuildIfDirty()` — пересобирает JSON через `JsonSerializer.SerializeToUtf8Bytes`, если dirty. Вызывается системой раз в секунду и в `GetStatusJson()` на случай пинга до первого тика.
- `GetStatusJson()` — отдаёт готовый `byte[]`. Вызывается из Gateway при Status Request.

## UpdateServerInfoSystem

**`UpdateServerInfoSystem : IProtoInitSystem, IProtoRunSystem`** — раз в `ServerConstants.SERVER_INFO_UPDATE_INTERVAL` (1 сек) вызывает `RebuildIfDirty()`. Накапливает `_timer` через `_serverTime.DeltaTime`, при превышении интервала сбрасывает и пересобирает. Так кэш держится свежим без аллокаций на каждом тике и на каждом пинге.

## Vanilla-реестры 26.2 — `SyncedRegistryCatalog`

`SyncedRegistryCatalog` — статический класс с canonical списками synced-реестров Minecraft 26.2 для Configuration (заменил удалённый `RegistryTagCatalog`). Источник данных — bytecode `RegistryDataLoader.SYNCHRONIZED_REGISTRIES`, извлечённый статически через `javap`.

- `RegistryIds` — 29 идентификаторов synced-реестров как готовые UTF-8 байты (`"minecraft:worldgen/biome"u8` и т.д.), без VarInt-префикса. Порядок массива совпадает с порядком отправки packet'ов Registry Data в Configuration. Контринтуитивное: Java-поле `Registries.BIOME` разворачивается в `minecraft:worldgen/biome` (с префиксом `worldgen/`) — единственный реестр с префиксом из 29. Поэтому список выведен из bytecode, а не из имён полей.
- `EntryIds` — entry-ids для обязательных реестров, index-aligned с `RegistryIds`. Все variant-реестры и `painting_variant` требуют ≥1 entry (клиент 26.2 валидирует non-empty); остальные представлены пустым массивом (count=0 в listing).

GC-free: горячий путь Configuration копирует байты в `PacketStreamWriter` без аллокаций. Flow отправки (29 × Registry Data, listing-only wire-формат 26.2) описан в [Gateway](../gateway/index.md).

## Типы игрока и DTO для Bridge

В `User/` лежат общие для слоёв `readonly struct` (чистые данные профиля игрока):

- **`UserProfile(Guid uuid, string username, string locale)`** — UUID, имя, локаль. Заполняется поэтапно: `uuid`+`username` в Login, `locale` — в Configuration.
- **`NetworkSession(int protocolVersion, string ipAddress, string serverAddress, ushort serverPort)`** — параметры подключения. Заполняется из Handshake в Gateway.

В `Bridge/Contracts/` лежит DTO для передачи игрока между слоями:

- **`EnterRealmHandoffData(UserProfile Profile, NetworkSession Session) : BridgeHandoffData`** — `sealed record`, наследник `BridgeHandoffData` из `Verstack.Shared.Bridge` (см. [Bridge](../bridge/index.md)). Gateway упаковывает профиль + сессию при передаче игрока в Realm; Realm вычитывает их в `UserEnterSystem`.

Контракт живёт именно в Layer.Global, а не в Gateway/Realm, потому что оба слоя должны видеть тип, а зависимость направлена только вниз: `Layer.Realm → Layer.Gateway → Layer.Global`. Так общий тип доступен обоим без цикла.

## ServerConstants / ServerTime

`ServerConstants` и `ServerTime` физически лежат в `Verstack.Lifecycle` (см. [Архитектуру](../architecture.md)), но конфигурируют GLOBAL-слой и инжектятся во все миры:

- `TICKS_PER_SECOND = 20`, `TICK_INTERVAL = 1.0 / 20` (50 мс) — для расчёта сна в `EntryPoint.RunMainLoop`.
- `SERVER_INFO_UPDATE_INTERVAL = 1.0` — интервал обновления кэша статуса.
- `COMPRESSION_THRESHOLD = 256` — порог сжатия (Set Compression, байты). `256` — стандарт ванили. Читается `LoginStartBundle` при отправке Set Compression.
- `ServerTime` — сервис, считает время через `Stopwatch.GetTimestamp()`. `DeltaTime` — время предыдущего тика; `TotalTime` считается напрямую от старта (`currentTimestamp * TickFrequency`), без накопления DeltaTime — чтобы избежать накопления погрешности (дрейфа). `Update()` вызывается в конце каждого тика.

## Связь с другими слоями

GLOBAL виден из Gateway и Realm через именованный мир `[DI(ServerWorldScopes.GLOBAL)]`. Точки контакта:

- Gateway → Global: `StatusExchangeBundle` достаёт `ServerInfoCacheStore` из GLOBAL-мира в `Init` и использует `GetStatusJson()` при Status Request; Configuration-бандлы читают `SyncedRegistryCatalog`.
- Realm → Global: системы Realm получают `[DI(ServerWorldScopes.GLOBAL)] IPacketCompressor` — компрессор живёт в GLOBAL (зарегистрирован `NetworkHubModule`), а используется в обоих фазовых слоях.
- Bridge handoff: `EnterRealmHandoffData` проходит через Bridge, но тип объявлен здесь.
