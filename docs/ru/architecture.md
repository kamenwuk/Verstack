# Архитектура

Карта кодовой базы Verstack: какие проекты есть, чем владеет каждый и в какую сторону идут зависимости. Детали реализации каждого слоя — на отдельных страницах.

## Структура решения

```
Verstack.slnx                              ← XML-формат решения .NET 10
Directory.Build.props                      ← общие настройки всех проектов
src/
├── Verstack.App/                          ← Program.cs, точка входа. AssemblyName=Verstack
├── Verstack.Debug/                        ← Logger (LogKey + LogLocale, i18n-словарь)
├── Verstack.ECS/                          ← завендоренный Leopotam.EcsProto + QoL. 0 NuGet
├── Verstack.NBT/                          ← NBT writer+reader: NbtWriter/NbtReader (ref struct), ModifiedUtf8
├── Verstack.Network/                      ← TCP/сокеты + фрейминг. Пассивный насос байт
├── Verstack.Lifecycle/                    ← жизненный цикл сервера: тик-луп, композиция слоёв, ServerFeatureLayer
├── Verstack.Shared.Bridge/                ← Мост: async-сеть ↔ sync-ECS + передача владения каналом между слоями
├── shared/Verstack.Shared.Assets/         ← адресация и ArrayPool-загрузка файлов assets/ (.nbt/.registry/.tags)
├── engine/Verstack.Engine.World/          ← модель чанков + сериализация в wire-формат протокола 26.2
├── Verstack.Layer.Global/                 ← GLOBAL-мир: MOTD, ServerInfo, каталог реестров 26.2
├── Verstack.Layer.Gateway/                ← GATEWAY-мир: Handshake, Status, Login, Configuration
└── Verstack.Layer.Realm/                  ← REALM-мир: фаза Play (вход в мир, чанки)
tools/
├── Verstack.DataCompiler/                 ← компилятор данных (ассеты → бинарные кэши)
└── Verstack.Probe/                        ← нагрузочный имитатор N клиентов
```

`Verstack.App` — корень композиции, единственная исполняемая сборка. `Verstack.Lifecycle` владеет серверным процессом: тик-лупом (`EntryPoint`), композицией ECS-миров (`ServerComposer`) и базовым классом слоя (`ServerFeatureLayer`). `Verstack.Shared.Bridge` — инфраструктурный клей между асинхронной сетью и синхронным ECS-тиком; через него же слои передают игрока по эстафете.

## Как идут зависимости

```
                       App
                        │
                        ▼
                    Lifecycle ──────► Shared.Bridge
                        │                 │
                        ▼                 ▼
            ┌───── Layer.Realm ─────► Network ─► ECS
            │           │              │
            │           ▼              ▼
      Engine.World   Layer.Gateway  Debug
            │           │
            └──► NBT ◄──┘
                        │
            ┌───────────┼───────────┐
            ▼           ▼           ▼
   Shared.Assets   Layer.Global   NBT
                        │
                        ▼
                  Network → ECS → Debug
```

Зависимости направлены вниз — к фундаменту. Пирамида слоёв: `Layer.Realm → Layer.Gateway → Layer.Global` (верхний слой знает нижние, но не наоборот). `Lifecycle` и `Shared.Bridge` — боковой инфраструктурный слой над сетью: они не знают про Minecraft-фазы, но связывают транспорт с ECS-тиком. `Shared.Assets` и `Engine.World` — утилитарные движки без ECS-зависимостей (Assets — чистый лист, Engine.World зависит только от NBT и Network).

| Слой | Может ссылаться на | НЕ может ссылаться на |
|---|---|---|
| `App` | Lifecycle, Debug | всё прикладное (тонкая точка входа) |
| `Lifecycle` | Network, ECS, Shared.Bridge | слои, фазы, Core-абстракции прикладных типов |
| `Shared.Bridge` | Network, ECS | слои, Lifecycle, прикладные типы игрока |
| `Layer.Realm` | Engine.World, NBT, ECS, Layer.Global (+ транзитивно Network, Shared.Bridge, Lifecycle) | Layer.Gateway напрямую |
| `Layer.Gateway` | Shared.Assets, Layer.Global, Lifecycle, Network, ECS, NBT (+ транзитивно Shared.Bridge) | Layer.Realm |
| `Layer.Global` | Lifecycle, Network, ECS | Layer.Gateway, Layer.Realm |
| `Engine.World` | NBT, Network | ECS, слои, Lifecycle |
| `Shared.Assets` | — (чистый лист, 0 зависимостей) | всё |
| `Network` | Debug, ECS | слои, Lifecycle, фазы |
| `NBT` / `ECS` / `Debug` | только BCL | всё прикладное |

- **Слои не лезут в сокеты напрямую.** Единственный путь байта в сеть — через `NetworkChannel`, который слой получает от Bridge (а Bridge — от `TcpNetworkService`). Бандл описывает исходящие пакеты через `PacketOutbound`; фрейминг и compression — забота транспорта. Network не знает про Minecraft-фазы.
- **Bridge связывает async и sync.** Поток accept'а в `TcpNetworkService` не трогает ECS-мир — он сообщает о подключении/отключении в `BridgeHandoffRouter` (через колбэк `ClientLifecycleHandler`), а тот складывает события в `ConcurrentQueue`. ECS-системы Bridge вычитывают их в тике. Так транспорт отделён от логики. См. [Bridge](bridge/index.md).
- **ECS — фундамент под слоями.** Завендоренный `Leopotam.EcsProto` (+QoL) лежит в `Verstack.ECS`, от него зависят слои, Bridge и Network. Не потокобезопасен — синхронизация делается ECS-системами.

## Слои и ECS-миры

Каждый слой — наследник `ServerFeatureLayer` (из `Verstack.Lifecycle`): объявляет скоуп своего ECS-мира, видимые чужие скоупы, следующий слой в цепочке handoff и политику передачи игрока. Скоуп — строковая константа в `ServerWorldScopes`:

| Скоуп (`ServerWorldScopes.*`) | Роль | Видит другие миры | Слой-наследник |
|---|---|---|---|
| `GLOBAL` | Данные, общие для всего сервера: MOTD, ServerInfo, каталог реестров | — (виден всем остальным) | `GlobalLayer` |
| `GATEWAY` | Вход: Handshake, Status, Login, Configuration | `GLOBAL` | `GatewayLayer` |
| `REALM` | Игровой мир: фаза Play | `GLOBAL` | `RealmLayer` |

`ServerComposer` (Lifecycle) принимает Global-слой и массив остальных, собирает для каждого `ProtoWorld` (через `AutoInjectModule(true)` + аспекты cache-stores) и настраивает видимость миров: мир слоя получает `AddWorld(foreignWorld, scope)` для каждого чужого скоупа из `GetVisibleScopes`. Сервисы (`ServerTime`) добавляются через `AddService` и инжектятся `[DI]` во все миры.

Передача владения каналом игрока между слоями настраивается в `EntryPoint`: `BridgeHandoffRouter` получает цепочку `GATEWAY → REALM` (REALM терминальный — `nextScope = ""`). Global в цепочке не участвует, он живёт как корневой слой. См. [Bridge](bridge/index.md).

## Главный тик

`EntryPoint.RunMainLoop` крутит фиксированный цикл 20 TPS (`ServerConstants.TICK_INTERVAL = 1/20`):

```
while (_isRunning):
    try:
        foreach (layer in _layers):
            layer.Run()           # каждый слой прогоняет свои ECS-системы
    catch Exception:              # тик не должен ронять сервер — лог и дальше
        Logger.Error(...)

    serverTime.Update()
    sleep до следующего тика (с мгновенным пробуждением по сигналу остановки)
```

EcsProto не потокобезопасен, поэтому accept-поток в `TcpNetworkService` **не трогает** мир — он только кладёт события подключения/отключения в `ConcurrentQueue` на `BridgeHandoffRouter`. Единственный писатель ECS-миров — ECS-системы в главном тике (включая четыре Bridge-системы на слой: Transfer → Cleanup → Intake → Disconnect). Пакеты копятся в `IncomingPackets` на каналах, пока их не вычитают системы слоя.

Каждый слой через `BridgeLayerModule` первым делом регистрирует четыре Bridge-системы в фиксированном порядке, затем — свои прикладные системы (`GuestScreeningSystem`, `PacketDispatchSystem` и т.д.). Так Bridge-логика (приём новых подключений, трансфер игрока дальше, чистка мусора) выполняется раньше фазовой.

## Слои

### Verstack.Network

Пассивный насос байт. `TcpNetworkService` владеет слушающим сокетом и accept-циклом: для каждого соединения создаёт `NetworkChannel` (Socket + PipeReader/Writer + `ConcurrentQueue<RawPacket>`), сообщает о нём в `ClientLifecycleHandler` (реализацию даёт Bridge) и запускает фоновое чтение и send-воркер. Чтение режет поток байт на `RawPacket` (packet id + payload) через `PacketFrame.TryRead` и складывает в очередь канала — без какой-либо семантики Minecraft. `Packet/Readers/` и `Packet/Writers/` содержат примитивы кодирования (VarInt, Numeric, Utf8String, Uuid, Vector2/3) как extension-методы к `PacketStreamReader`/`PacketStreamWriter` (`ref struct`). `Packet/Pipeline/` — каркас конвейера бандлов: `PacketBundle`, `SequentialPacketPipeline`, `DispatchPacketPipeline`, `PacketFlowState`. `Compression/` — `IPacketCompressor`/`IPacketDecompressor` и zlib-реализации; фрейминг переключается на compressed-формат после Set Compression на канале.

→ [Network](network/index.md)

### Verstack.Shared.Bridge

Мост между асинхронным сетевым слоем и синхронным ECS-тиком, и одновременно — механизм передачи владения каналом игрока между ECS-мирами. `BridgeHandoffRouter` (наследник `ClientLifecycleHandler` из Network) принимает TCP-события в фоновых потоках и хранит ownership канал→скоуп. Четыре ECS-системы на слой (Transfer/Cleanup/Intake/Disconnect), подключаемые `BridgeLayerModule`, вычитывают события в тике и ведут конечный автомат сущности `Pending → Connected → Disconnected`. `BridgeHandoffPolicy` — абстрактная политика готовности слоя передать игрока дальше (реализуется слоем). `BridgeStateCacheStore` — аспект на слой: фильтры по состояниям + маппинг entity↔channel + handoff-очередь DTO.

→ [Bridge](bridge/index.md)

### Verstack.Lifecycle

Жизненный цикл серверного процесса. `EntryPoint` — запуск (`Start(port, globalLayer, layers)` строит Bridge-роутер, `NetworkHubModule`, композит; запускает TCP-слушатель и главный тик) и остановка (`Stop()` будит тик через `CancellationToken`, destroy'ит миры). `ServerComposer` — трёхфазная сборка: создание `ProtoSystems` на слой, настройка видимости миров, `Init`. `ServerFeatureLayer` — базовый класс слоя. `ServerWorldScopes` — константы имён миров. `ServerTime`/`ServerConstants` — время и константы (TPS, порог compression).

### Verstack.Layer.Global

GLOBAL-мир. `ServerInfoCacheStore` — аспект с dirty-flag: MOTD/версия/слоты хранятся как поля, JSON статуса пересобирается только при изменении и кэшируется в `byte[]`. На пинге сервер-листа — нулевые аллокации. `UpdateServerInfoSystem` раз в секунду проверяет dirty и пересобирает кэш. `SyncedRegistryCatalog` — 29 synced-реестров Minecraft 26.2 и их обязательные entry-ids. `Bridge/Contracts/EnterRealmHandoffData` — DTO (record), который Gateway упаковывает при передаче игрока в Realm; `User/UserProfile` и `User/NetworkSession` — типы данных игрока, общие для слоёв.

→ [Global](global/index.md)

### Verstack.Layer.Gateway

GATEWAY-мир, входной слой. `GuestScreeningSystem` вычитывает новых игроков из Bridge (`TryDequeueHandoff`), парсит Handshake и разводит: Status (bundleIndex 0) или Login (bundleIndex 2), добавляя `NetworkSession` + `PacketFlowState` на сущность. `PacketDispatchSystem` гоняет пакеты активных сессий через `SequentialPacketPipeline` — конвейер из 7 бандлов (Status ×2, Login ×2, Configuration ×3). Когда фазы пройдены (pipeline вернул `Transfer`), `GatewayHandoffPolicy` в Bridge-системе трансфера упаковывает `EnterRealmHandoffData` и передаёт игрока в REALM — без закрытия сокета. `GatewayCacheStore` — аспект: пулы `Sessions`/`UserProfiles`/`FlowStates` + фильтр активных сессий.

→ [Gateway](gateway/index.md)

### Verstack.Layer.Realm

REALM-мир, фаза Play. `UserEnterSystem` — оркестратор входа: вычитывает handoff-очередь из Bridge (игрок пришёл из Gateway с `EnterRealmHandoffData`), наполняет кэш сессии и прогоняет исходящую последовательность из 7 бандлов через `SequentialPacketPipeline` (Login(Play) → Spawn Position → Player Info → Commands → Game Event → Chunk Batch → Synchronize Position). `SessionPacketRouterSystem` — маршрутизатор входящих play-пакетов через `DispatchPacketPipeline` (Confirm Teleport, Set Player Position, Set Player Position and Rotation). Чанки — из `Verstack.Engine.World` через `FlatGenerator`. `RealmNetworkHandoffPolicy` — no-op (Realm терминальный).

→ [Realm](realm/index.md)

### Verstack.Engine.World

Игровой движок мира. `Chunk` (24 секции по 16³ блоков) и `ChunkSection` (плоский массив 4096 block-state-id, сериализация в wire-формат протокола 26.2: single-value или direct palette, heightmaps, полный свет). `FlatGenerator` — тестовый генератор плоского мира. `ChunkManager` — zero-alloc кэш чанков через `CollectionsMarshal.GetValueRefOrAddDefault`. Используется только из Realm.

→ [Engine.World](engine-world/index.md)

### Verstack.Shared.Assets

Чистый лист, 0 зависимостей. Адресация файлов `assets/` (`AssetCatalog`/`AssetSource`: `assets/{catalog}/{asset}/{name}{ext}`) и их загрузка через `ArrayPool` двумя режимами: одноразовая batch-загрузка тегов (`PreloadTagBatch` — для Update Tags в Configuration) и долговременный кэш с арендой (`PreloadCached`/`GetCached`). `ScopedAssetBuffer` — `ref struct` для `using`-блока, читает через `RandomAccess.Read` напрямую в пул-буфер.

### Verstack.NBT

NBT writer + reader (Named Binary Tag): `NbtWriter` (`ref struct`, GC-free запись прямо в `Span<byte>` через стек `NbtFrame`) и `NbtReader` (зеркало, sequental-core + lookup), `ModifiedUtf8` (Java modified-UTF-8 в обе стороны), networked-root по умолчанию. Симметричный writer/reader через один `NbtFrame`-стек. DOM отложена. BCL-лист, 0 зависимостей.

→ [NBT](nbt/index.md)

### Verstack.Debug / ECS

`Debug` — `Logger` с i18n через `LogKey` + `LogLocale`. `ECS` — завендоренный `Leopotam.EcsProto` (+QoL), фундамент под слоями.

`Verstack.ECS` — единственный сторонний код в проекте, лицензирован под **MIT-ZARYA** ([LICENSE.md](../../src/Verstack.ECS/LICENSE.md)). MIT-ZARYA разрешает использование и распространение с одним условием: если ПО локализовано на несколько языков, обязательна локализация на Русский язык, не менее полная, чем на любом другом. Verstack этому соответствует — `docs/ru/` и `README.ru.md` зеркальны английским. Файл лицензии включается в выходные артефакты сборки `Verstack.ECS`.

## Текущий статус

- ✅ ECS-ядро: завендорен Leopotam.EcsProto + QoL, три мира (Global/Gateway/Realm), `AutoInjectModule`/`[DI]`.
- ✅ Главный тик 20 TPS с try/catch и мгновенной остановкой по сигналу.
- ✅ Network: `TcpNetworkService` (accept → ConcurrentQueue), `NetworkChannel`, фрейминг `PacketFrame` с compression, `PacketStreamReader`/`PacketStreamWriter`, каркас `PacketBundle`/`Sequential`/`DispatchPacketPipeline`.
- ✅ Bridge: async-сеть ↔ sync-ECS, конечный автомат сущности, передача владения каналом между слоями без закрытия сокета.
- ✅ Gateway/Global: пинг сервер-листа отвечает MOTD/версией/слотами через GLOBAL-кэш с нулевыми аллокациями.
- ✅ Handshake: парсинг, заполнение `NetworkSession` данными (protocolVersion, IP, serverAddress, serverPort).
- ✅ Status: полный пинг-обмен (Request → JSON Response, Ping → Pong) через конвейер бандлов, на сущности.
- ✅ Login: offline-флоу — Login Start → Set Compression → Login Success → Login Acknowledged. Offline UUID v3 от `"OfflinePlayer:<name>"`, поле Session ID протокола 776.
- ✅ Compression: zlib (RFC 1950) фрейминг в обе стороны, per-channel threshold (256, стандарт ванили), GC-free холодный путь. Включается после Set Compression.
- ✅ Configuration: полный флоу — Client Information → Known Packs → Registry Data × 29 → Update Tags → Feature Flags → Finish → Acknowledge. Registry Data (S→C 0x07) listing-only, Update Tags (S→C 0x08) пакетно из `AssetSource`. После Finish — handoff в Realm.
- ✅ NBT: GC-free writer + reader — `NbtWriter`/`NbtReader` (`ref struct`, `Span<byte>`, стек `NbtFrame`), `ModifiedUtf8`, networked-root. DOM отложена.
- ✅ Engine.World: модель чанков (24 секции × 16³), сериализация в wire-формат протокола 26.2 (single-value/direct palette, heightmaps, полный свет), `FlatGenerator`.
- 🔨 Realm: фаза Play — вход в мир реализован (7 исходящих бандлов: Login(Play), Spawn Position, Player Info, Commands, Game Event, Chunk Batch 5×5, Synchronize Position), маршрутизатор входящих пакетов (Confirm Teleport, Position, Position+Rotation). Физика/движение заглушены (TODO `MoveRequestComponent`), keep-alive закомментирован.
