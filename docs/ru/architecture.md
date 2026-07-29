# Архитектура

Карта кодовой базы Verstack: какие проекты есть, чем владеет каждый и в какую сторону идут зависимости. Детали реализации каждого слоя — на отдельных страницах.

## Структура решения

```
Verstack.slnx                          ← XML-формат решения .NET 10
Directory.Build.props                  ← общие настройки всех проектов
src/
├── Verstack.App/                      ← Program.cs, точка входа. AssemblyName=Verstack
├── Verstack.Bootstrap/                ← композиция: ServerComposer + EntryPoint (главный тик-луп)
├── Verstack.Core/                     ← базовые абстракции: VerstackFeature, WorldScopes, ServerTime
├── Verstack.Debug/                    ← Logger (LogKey + LogLocale, i18n-словарь)
├── Verstack.ECS/                      ← завендоренный Leopotam.EcsProto + QoL. 0 NuGet
├── Verstack.NBT/                      ← NBT writer+reader: NbtWriter/NbtReader (ref struct), ModifiedUtf8, networked-root
├── Verstack.Network/                  ← TCP/сокеты + фрейминг. Пассивный насос байт
├── Verstack.Layer.Global/             ← GLOBAL-мир: MOTD, ServerInfo, константы
├── Verstack.Layer.Gateway/            ← GATEWAY-мир: Handshake, Status, Login, Configuration
└── Verstack.Layer.Realm/              ← REALM-мир: фаза Play (запланирован, пока пуст)
tools/
└── Verstack.Probe/                    ← нагрузочный имитатор N клиентов
```

## Как идут зависимости

```
                    App
                     │
                     ▼
                  Bootstrap
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   Layer.Realm   Network      Layer.Global
        │            │            │
        ▼            ▼            ▼
   Layer.Gateway  Verstack.ECS  Verstack.Core
        │            │            │
        ▼            ▼            ▼
   Layer.Global  (BCL only)    Verstack.Debug
        │                       (BCL only)
        ▼
   Layer.Global → Core → Debug
```

Зависимости линейные, направлены вниз — к фундаменту. `App` — корень композиции, единственная исполняемая сборка. `Bootstrap` собирает из Feature-ов три ECS-мира и сервисы, крутит главный тик. `Layer.Realm → Layer.Gateway → Layer.Global → Core` — пирамида слоёв: верхний слой знает нижние, но не наоборот.

`Verstack.ECS` и `Verstack.Debug` — листья: `ECS` зависит только от BCL, `Debug` — тоже. `Verstack.NBT` — тоже лист, зависит только от BCL. `Verstack.Network` зависит от `ECS` (типы `RawPacket`/`PacketBundle` используют `ProtoEntity`) и `Debug` (логирование).

| Слой             | Может ссылаться на                                  | НЕ может ссылаться на                       |
|------------------|-----------------------------------------------------|---------------------------------------------|
| `App`            | Bootstrap, ECS, Network                             | — (корень композиции)                       |
| `Bootstrap`      | Debug, ECS, NBT, Network, Core, Layer.Global/Gateway/Realm | — (точка сборки)                            |
| `Layer.Realm`    | ECS, Core, Layer.Gateway                            | Network (напрямую), Layer.Global (транзитивно через Gateway) |
| `Layer.Gateway`  | ECS, Core, Layer.Global, Network                    | Layer.Realm                                 |
| `Layer.Global`   | ECS, Core                                           | Network, Layer.Gateway, Layer.Realm         |
| `Network`        | Debug, ECS                                          | слои, Core, Minecraft-фазы                  |
| `Core`           | Debug, ECS                                          | слои, Network                               |
| `ECS` / `Debug` / `NBT` | только BCL                                   | всё прикладное                              |

- **Слои не лезут в сокеты напрямую.** Единственный путь байта в сеть — через `NetworkChannel`, который слой получает от `TcpNetworkService`. Бандл описывает исходящие пакеты через `PacketOutbound` (`ref struct` поверх heap-буферов); фрейминг и compression — забота транспорта, а не бандла. Network не знает про Minecraft-фазы.
- **ECS — фундамент под слоями.** Завендоренный `Leopotam.EcsProto` (+QoL) лежит в `Verstack.ECS`, от него зависят все слои и Network. Не потокобезопасен — синхронизация делается ECS-системами (см. ниже развязку с сетью).

## ECS-миры и их видимость

Три изолированных ECS-мира, по одному на логический скоуп. Имена — константы в `WorldScopes`:

| Скоуп (`WorldScopes.*`) | Роль                                                    | Видит другие миры            |
|------------------------|---------------------------------------------------------|------------------------------|
| `GLOBAL`               | Данные, общие для всего сервера: MOTD, ServerInfo, время | — (виден всем остальным)     |
| `GATEWAY`              | Вход: Handshake, Status, Login, Configuration           | `GLOBAL`                     |
| `REALM`                | Игровой мир: фаза Play (запланирован)                   | `GLOBAL`, `GATEWAY`          |

Сборка миров — в `ServerComposer`: каждый Feature (`GlobalFeature`, `GatewayFeature`, `RealmFeature`) регистрирует свои аспекты (`ProtoAspectInject`-сторы) и системы. Сервисы (`TcpNetworkService`, `ServerTime`) добавляются через `AddService` и инжектятся `[DI]` во все миры. `AutoInjectModule(true)` включает инъекцию и в сервисы.

## Главный тик

`EntryPoint.RunMainLoop` крутит фиксированный цикл 20 TPS (`ServerConstants.TICK_INTERVAL = 1/20`):

```
while (_isRunning):
    try:
        globalSystems.Run()       # всегда: MOTD, время, метрики
        gatewaySystems.Run()      # можно поставить на паузу (DDoS-backpressure)
        # realmSystems.Run()      # всегда: фаза Play, игроки не замечают атаку
    catch Exception:              # тик не должен ронять сервер — лог и дальше
        Logger.Error(...)

    serverTime.Update()
    sleep до следующего тика (с мгновенным пробуждением по сигналу остановки)
```

Ключевая идея backpressure'а: при DDoS-атаке на Gateway (`gatewaySystems.Run()` пропускается), сокеты в `TcpNetworkService` продолжают принимать пакеты и складывают их в `ConcurrentQueue<RawPacket>` на каналах. Когда пауза снимается — пакеты вычитываются. Realm при этом тикает без остановки, игроки в игре атаки не замечают. EcsProto не потокобезопасен, поэтому accept-поток в `TcpNetworkService` **не трогает** мир — он только кладёт `RawPacket` в очередь; единственный писатель мира — ECS-система в главном тике.

## Слои

### Verstack.Network

Пассивный насос байт. `TcpNetworkService` владеет слушающим сокетом и accept-циклом: для каждого соединения создаёт `NetworkChannel` (Socket + PipeReader/Writer + `ConcurrentQueue<RawPacket>`), кидает его в `PendingConnections` и запускает фоновое чтение. Чтение режет поток байт на `RawPacket` (packet id + payload) через `PacketFrame.TryRead` и складывает в очередь канала — без какой-либо семантики Minecraft. `DataTypes/` содержит примитивы кодирования (VarInt, Numeric, Utf8String, Uuid, PrefixedArray и т.д.). `Packet/` содержит фрейминг и каркас конвейера: `PacketFrame`/`PacketFrameResult` (compression-aware framing), `PacketOutbound`/`SpanWriter` (GC-free outbound для бандлов), `RawPacket`, `PacketBundle`, `PacketPipeline`, `PacketFlowState`. `Compression/` — абстракции `IPacketCompressor`/`IPacketDecompressor` и zlib-реализации по умолчанию; фрейминг переключается на compressed-формат после Set Compression на канале.

→ [Network](network/index.md)

### Verstack.Layer.Global

GLOBAL-мир. `ServerInfoCacheStore` — аспект с dirty-flag: MOTD/версия/слоты хранятся как поля, JSON статуса пересобирается только при изменении и кэшируется в `byte[]`. На пинге сервер-листа — нулевые аллокации, отдаётся готовый массив. `UpdateServerInfoSystem` раз в секунду проверяет dirty и пересобирает кэш. `ServerTime` — DeltaTime/TotalTime через `Stopwatch.GetTimestamp`, без дрейфа.

→ [Global](global/index.md)

### Verstack.Layer.Gateway

GATEWAY-мир, входной слой. `GuestScreeningSystem` принимает новые каналы из `PendingConnections`, парсит Handshake и разводит: Status (пинг/MOTD обслуживает тут же, без ECS-сущности) или Login (создаёт ECS-сущность с `NetworkSession` + `PacketFlowState`). `PacketDispatchSystem` гоняет пакеты залогиненных сессий через `GatewayPacketPipeline` — конвейер из `PacketBundle`'ов, где каждый бандл — фаза (Login, Configuration). `GatewayCacheStore` — аспект: пулы `Sessions`/`FlowStates` + side-словари entity↔channel.

→ [Gateway](gateway/index.md)

### Verstack.Layer.Realm

REALM-мир, фаза Play. Зарезервирован, `RealmFeature` пока пуст: `Init` без систем, `GetCacheStores()` → `[]`. Будет играть на 20 TPS независимо от нагрузки на Gateway.

### Verstack.Bootstrap

Композиция. `ServerComposer` принимает три Feature'а, собирает из их аспектов три `ProtoWorld` (через `ProtoModules` + `AutoInjectModule`), регистрирует сервисы и связывает миры по видимости. `EntryPoint` — жизненный цикл: `Start(port)` инициализирует сервисы и миры, запускает TCP-слушатель и главный тик; `Stop()` будит тик через `CancellationToken`, останавливает сеть и destroys миры.

### Verstack.NBT

NBT writer + reader (Named Binary Tag): `NbtWriter` (`ref struct`, GC-free запись прямо в `Span<byte>` через стек `NbtFrame`) и `NbtReader` (зеркало, sequental + lookup), `ModifiedUtf8` (Java modified-UTF-8 в обе стороны), networked-root по умолчанию. Симметричный writer/reader через один `NbtFrame`-стек. DOM отложена. BCL-лист, 0 зависимостей.

→ [NBT](nbt/index.md)

### Verstack.Core / Debug / ECS

`Core` — базовые абстракции: `VerstackFeature` (контракт Feature'а), `WorldScopes` (имена миров), `ServerTime`. `Debug` — `Logger` с i18n через `LogKey` + `LogLocale`. `ECS` — завендоренный `Leopotam.EcsProto` (+QoL), фундамент под слоями.

`Verstack.ECS` — единственный сторонний код в проекте, лицензирован под **MIT-ZARYA** ([LICENSE.md](../../src/Verstack.ECS/LICENSE.md)). MIT-ZARYA разрешает использование и распространение с одним условием: если ПО локализовано на несколько языков, обязательна локализация на Русский язык, не менее полная, чем на любом другом. Verstack этому соответствует — `docs/ru/` и `README.ru.md` зеркальны английским. Файл лицензии включается в выходные артефакты сборки `Verstack.ECS`.

## Текущий статус

- ✅ ECS-ядро: завендорен Leopotam.EcsProto + QoL, три мира (Global/Gateway/Realm), `AutoInjectModule`/`[DI]`.
- ✅ Главный тик 20 TPS с try/catch и мгновенной остановкой по сигналу.
- ✅ Network: `TcpNetworkService` (accept → ConcurrentQueue), `NetworkChannel`, фрейминг `PacketFrame` с `PacketOutbound` для бандлов. Пассивный, развязка потоков и ECS.
- ✅ Gateway/Global: пинг сервер-листа отвечает MOTD/версией/слотами через GLOBAL-кэш с нулевыми аллокациями.
- ✅ Handshake: парсинг, заполнение `NetworkSession` данными (protocolVersion, IP, serverAddress, serverPort).
- ✅ Status: полный пинг-обмен (Request → JSON Response, Ping → Pong) через конвейер бандлов, на сущности.
- ✅ Login: offline-флоу — Login Start → Set Compression → Login Success → Login Acknowledged. Offline UUID v3 от `"OfflinePlayer:<name>"`, поле Session ID протокола 776. После завершения фазы канал закрывается (REALM/Configuration пока не реализованы).
- ✅ Compression: zlib (RFC 1950) фрейминг в обе стороны, per-channel threshold (256, стандарт ванили), GC-free холодный путь. Включается после Set Compression.
- ✅ NBT: GC-free writer + reader — `NbtWriter`/`NbtReader` (`ref struct`, `Span<byte>`, стек `NbtFrame`), `ModifiedUtf8` (в обе стороны), networked-root. Reader: sequental-core + lookup по имени. DOM отложена.
- 🔨 Configuration: каркас (ClientInformation → KnownPacks → Registry Data × 29 → Feature Flags → Finish → Disconnect) работает; Registry Data (S→C 0x07) отправляется listing-only (29 synced-реестров 26.2), принят клиентом 26.2. Не хватает Update Tags (S→C 0x08) — клиент падает на валидации тегов; это следующая задача.
- 🔨 Realm: фаза Play не реализована, слой пуст.
