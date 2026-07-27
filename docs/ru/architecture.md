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
├── Verstack.NBT/                      ← NBT (запланирован, пока пуст)
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

`Verstack.ECS` и `Verstack.Debug` — листья: `ECS` зависит только от BCL, `Debug` — тоже. `Verstack.NBT` пока пуст, зависимости не имеет. `Verstack.Network` зависит от `ECS` (типы `RawPacket`/`PacketBundle` используют `ProtoEntity`) и `Debug` (логирование).

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

- **Слои не лезут в сокеты напрямую.** Единственный путь байта в сеть — через `NetworkChannel`, который слой получает от `TcpNetworkService` и в который пишет ответ. Network не знает про Minecraft-фазы.
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

Пассивный насос байт. `TcpNetworkService` владеет слушающим сокетом и accept-циклом: для каждого соединения создаёт `NetworkChannel` (Socket + PipeReader/Writer + `ConcurrentQueue<RawPacket>`), кидает его в `PendingConnections` и запускает фоновое чтение. Чтение режет поток байт на `RawPacket` (packet id + payload) и складывает в очередь канала — без какой-либо семантики Minecraft. `DataTypes/` содержит примитивы кодирования (VarInt, Numeric, Utf8String и т.д.), `Packet/` — каркас конвейера (`RawPacket`, `PacketBundle`, `PacketPipeline`, `PacketFlowState`).

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

### Verstack.Core / Debug / ECS / NBT

`Core` — базовые абстракции: `VerstackFeature` (контракт Feature'а), `WorldScopes` (имена миров), `ServerTime`. `Debug` — `Logger` с i18n через `LogKey` + `LogLocale`. `ECS` — вендор Leopotam. `NBT` — запланирован.

## Текущий статус

- ✅ ECS-ядро: завендорен Leopotam.EcsProto + QoL, три мира (Global/Gateway/Realm), `AutoInjectModule`/`[DI]`.
- ✅ Главный тик 20 TPS с try/catch и мгновенной остановкой по сигналу.
- ✅ Network: `TcpNetworkService` (accept → ConcurrentQueue), `NetworkChannel`, фрейминг `RawPacket`. Пассивный, развязка потоков и ECS.
- ✅ Gateway/Global: пинг сервер-листа отвечает MOTD/версией/слотами через GLOBAL-кэш с нулевыми аллокациями.
- ✅ Handshake: парсинг, заполнение `NetworkSession` данными (protocolVersion, IP, serverAddress, serverPort).
- 🔨 Login/Configuration: каркас `PacketBundle`/`PacketPipeline` готов, бандлы не написаны — пакеты от залогиненного игрока пока приводят к кику.
- 🔨 Realm: фаза Play не реализована, слой пуст.
- ⏳ Send-сторона: синхронный `FlushAsync().GetAwaiter().GetResult()` в ECS-системах — узкое место backpressure, запланирован переход на send-очередь.
