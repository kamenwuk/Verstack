# Слой Global

Global — GLOBAL-мир в ECS, виден всем остальным мирам (Gateway, Realm). Содержит данные и подсистемы, общие для всего сервера: статус для пинга сервер-листа (MOTD/версия/слоты), серверное время, константы. Здесь нет ничего специфичного для фазы или соединения — только глобальное состояние.

Сборку делает `GlobalFeature : VerstackFeature` — регистрирует аспект `ServerInfoCacheStore` и систему `UpdateServerInfoSystem`. Feature подключается в `ServerComposer`. GLOBAL-мир тикает всегда, даже когда Gateway/Realm на паузе (DDoS-backpressure) — см. [Архитектуру](../architecture.md).

## ServerInfoCacheStore

**`ServerInfoCacheStore : ProtoAspectInject`** — аспект мира. Хранит поля статуса сервера: `Motd`, `MaxPlayers`, `VersionName`, `ProtocolVersion`, `OnlinePlayers`. Главная идея — нулевые аллокации на пинге сервер-листа: JSON статуса пересобирается только при изменении и кэшируется в `byte[] _cachedStatusJson`.

Механика через dirty-flag:

- `SetOnlinePlayers(count)` — вызывается системой, когда игрок заходит или выходит. Если значение не изменилось — ничего не делает. Если изменилось — ставит `_isDirty = true`, ничего не аллоцируя.
- `RebuildIfDirty()` — пересобирает JSON через `JsonSerializer.SerializeToUtf8Bytes`, если dirty. Вызывается системой раз в секунду и в `GetStatusJson()` на случай пинга до первого тика.
- `GetStatusJson()` — отдаёт готовый `byte[]`. Вызывается из Gateway при Status Request.

Конструктор принимает начальные значения: `new ServerInfoCacheStore("A Minecraft Server", 100, "26.2", 776)`. `26.2`/`776` — версия Minecraft 1.21.x.

## UpdateServerInfoSystem

**`UpdateServerInfoSystem : IProtoInitSystem, IProtoRunSystem`** — раз в `ServerConstants.SERVER_INFO_UPDATE_INTERVAL` (1 сек) вызывает `RebuildIfDirty()`. Накапливает `_timer` через `_serverTime.DeltaTime` (инжектированный `ServerTime`), при превышении интервала сбрасывает и пересобирает. Так кэш держится свежим без аллокаций на каждом тике и на каждом пинге.

## ServerConstants

Статический класс с базовыми константами сервера:

- `TICKS_PER_SECOND = 20` — стандартный TPS Minecraft.
- `TICK_INTERVAL = 1.0 / 20` — длительность тика в секундах (50 мс). Используется в `EntryPoint.RunMainLoop` для расчёта сна.
- `SERVER_INFO_UPDATE_INTERVAL = 1.0` — интервал обновления кэша статуса.
- `COMPRESSION_THRESHOLD = 256` — порог сжатия пакетов (Set Compression, байты). Пакеты размером ≥ threshold сжимаются (zlib); меньшие уходят несжатыми, но всё равно в формате compressed-фрейминга (`DataLength = 0`). `256` — стандарт ванили. `-1` или отсутствие пакета Set Compression отключает compression. Читается `LoginStartBundle` при отправке Set Compression.

## ServerTime

**`ServerTime`** — сервис, инжектится `[DI]` в системы. Считает время через `Stopwatch.GetTimestamp()` (высокоточный процессорный таймер):

- `DeltaTime` — время предыдущего тика в секундах.
- `TotalTime` — общее время работы сервера. Считается напрямую от старта (`currentTimestamp * TickFrequency`), без накопления DeltaTime — чтобы избежать накопления погрешности (дрейфа).

`Update()` вызывается в конце каждого тика в `EntryPoint.RunMainLoop`. Не ECS-компонент, а обычный класс-сервис — потому что время едино для всех миров и не привязано к сущности.

## Vanilla-реестры 26.2

В Layer.Global лежат canonical списки synced-реестров Minecraft 26.2 для Configuration: `VanillaSyncedRegistries` и `VanillaRegistryEntries`. Это статические данные — обычные классы с `byte[][]`/`byte[][][]`, инициализируемые один раз при первом обращении.

`VanillaSyncedRegistries.SyncedRegistryIds` — 29 идентификаторов synced-реестров как готовые UTF-8 байты (без VarInt-префикса). Источник — bytecode `RegistryDataLoader.SYNCHRONIZED_REGISTRIES`, извлечённый статически через `javap`. Порядок массива совпадает с порядком отправки packet'ов Registry Data в Configuration.

`VanillaRegistryEntries.EntryIds` — entry-ids для 13 обязательных реестров, index-aligned с `SyncedRegistryIds`. Все variant-реестры и `painting_variant` требуют ≥1 entry (клиент 26.2 валидирует non-empty); остальные 16 представлены пустым массивом (count=0 в listing). Источник — bundled-datapack клиент-jar, порядок внутри реестра alphabetical.

Контринтуитивное: Java-поле `Registries.BIOME` разворачивается в `minecraft:worldgen/biome` (с префиксом `worldgen/`), а не в `minecraft:biome` — единственный реестр с префиксом из 29. Поэтому список выведён из bytecode, а не из имён полей.

GC-free: горячий путь Configuration копирует байты в `SpanWriter` без аллокаций. Flow отправки (29 × Registry Data, listing-only wire-формат 26.2) описан в [Gateway](../gateway/index.md).

## Связь с другими слоями

GLOBAL виден из Gateway и Realm через именованный мир `[DI(WorldScopes.GLOBAL)]`. Точка контакта Gateway → Global — `GatewayIntakeHandler`, который в `Init` достаёт `ServerInfoCacheStore` из GLOBAL-мира через `world.Aspect<ServerInfoCacheStore>()` и использует его `GetStatusJson()` при Status Request. Realm будет обращаться к Global аналогично — когда появятся системы фазы Play.
