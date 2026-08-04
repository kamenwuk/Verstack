# Слой Bridge

Bridge («Мост») — инфраструктурный клей между асинхронным сетевым слоем и синхронным ECS-тиком. Две задачи: (1) безопасно прокидывать TCP-события подключения/отключения из фоновых потоков в ECS-тики; (2) передавать владение каналом игрока между ECS-мирами (слоями) без закрытия сокета. Не знает про Minecraft-фазы — чистая механика ownership'а.

Зависимости: `Verstack.Network` (тип `NetworkChannel`, базовый `ClientLifecycleHandler`) и `Verstack.ECS`. Сам Bridge зависит только от фундамента, а слои (Gateway, Realm) получают его типы транзитивно через Layer.Global / Network. `Verstack.Lifecycle` ссылается на Bridge напрямую.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## Центральный хаб — `BridgeHandoffRouter`

`BridgeHandoffRouter : ClientLifecycleHandler` — `public sealed class`, реализация хуков connect/disconnect из Network. Потокобезопасный (`Lock _lock`). Конструируется в `EntryPoint.Start` с дефолтным скоупом (GATEWAY) и настраивается цепочкой переходов:

```csharp
var router = new BridgeHandoffRouter(ServerWorldScopes.GATEWAY);
router.AddLayer(ServerWorldScopes.GATEWAY, ServerWorldScopes.REALM);
router.AddLayer(ServerWorldScopes.REALM, string.Empty);   // REALM терминальный
```

Хранит:

- `_handoffMap` — цепочка переходов scope → nextScope.
- `_ownership` — текущий ownership channel → scope (какому миру принадлежит сокет прямо сейчас).
- `_pending` — `ConcurrentQueue<NetworkChannel>` новых подключений, ждущих Intake-системы.
- `_disconnected` — `ConcurrentQueue<NetworkChannel>` отключившихся каналов, ждущих Disconnect-системы.

`HandleConnect(channel)` (вызывается из accept-потока в `TcpNetworkService`) помечает ownership = дефолтный скоуп и кладёт канал в `_pending`. `HandleDisconnect(channel)` — в `_disconnected`. `TransferToNext(entity, channel, data, ...)` (вызывается Bridge-системой трансфера в ECS-тике) перекладывает ownership в nextScope и кладёт `PendingTransfer` в очередь приёмного слоя. Сокет при трансфере **не** закрывается.

## Компоненты-состояния сущности

Три ECS-компонента-маркера (`readonly struct`) образуют конечный автомат сущности игрока:

- **`BridgeHandoffPending`** — «игрок создан в ECS, но ожидает инициализации специфичными системами текущего слоя». Intake-система ставит его при создании сущности; фазовая система (напр. `GuestScreeningSystem`) снимает после promotion.
- **`BridgeClientConnected`** — «на рельсах»: активный игрок в текущем слое. `ConnectedFilter` кэш-стора ловит такие сущности.
- **`BridgeClientDisconnected`** — маркер отключения: канал упал, сущность ждёт удаления.

## Данные — `BridgeHandoffData`

`BridgeHandoffData` — `public abstract record`, базовый класс DTO между ECS-мирами. Комментарий в коде: «наследники должны лежать в общих контрактах и содержать только данные, необходимые принимающей стороне». В Verstack единственный наследник — `EnterRealmHandoffData` в `Verstack.Layer.Global.Bridge.Contracts` (см. [Global](../global/index.md)).

`HandoffPayload(ProtoEntity entity, BridgeHandoffData data)` — `public readonly struct`, обёртка при `TryDequeueHandoff`: сущность + данные, которые слой-отправитель упаковал.

`PendingTransfer(NetworkChannel, BridgeHandoffData)` — `internal readonly record struct`, элемент очереди роутера.

## CacheStore — `BridgeStateCacheStore`

`BridgeStateCacheStore : ProtoAspectInject` — аспект, регистрируемый в **каждом** слое (Global — исключение, в нём нет игроков). Хранит:

- `ConnectedFilter` / `DisconnectedFilter` / `PendingGarbageFilter` (internal) — `ProtoIt` по маркерам состояний.
- `_entityToChannel` / `_channelToEntity` — маппинги для `GetChannel(entity)` и обратного поиска при дисконнекте.
- `_handoffQueue` — FIFO-очередь `HandoffPayload`, которую фазовые системы вычитывают через `TryDequeueHandoff(out HandoffPayload)`.

Основной публичный API для прикладных систем: `GetChannel(ProtoEntity) → NetworkChannel`, `TryDequeueHandoff(out HandoffPayload) → bool`, фильтры `ConnectedFilter`/`DisconnectedFilter`.

## Четыре ECS-системы на слой — `BridgeLayerModule`

`BridgeLayerModule(scope, nextScope, handoffRouter, handoffPolicy) : IProtoModule` внедряется в каждый слой **первым** (до фазовых систем) и жёстко задаёт порядок четырёх систем в `Run`:

1. **`BridgeTransferSystem`** — первой в тике. Если `nextScope` не пуст, для каждой сущности в `ConnectedFilter` спрашивает у политики `TryTransfer(entity, channel, out data)`. Если `true` — вызывает `router.TransferToNext`, удаляет сущность из текущего мира. **Сокет не закрывает** (`RemoveChannel(entity, closeSocket: false)`): канал остаётся живым, ownership перешёл к приёмному слою.
2. **`BridgeCleanupSystem`** — удаляет «мусор»: зависшие в Pending (клиент подключился и тут же отвалился, пока лежал в очереди — Intake такую сущность не создаст) и отвалившиеся активные сущности. Тут сокет **закрывается** (`closeSocket: true`).
3. **`BridgeIntakeSystem`** — вычитывает новые подключения/трансферы из роутера (`TryDequeuePending`), создаёт ECS-сущности в состоянии `BridgeHandoffPending` (+ `BridgeClientConnected`), регистрирует entity↔channel. Комментарий про гонку: «если клиент подключился и мгновенно отвалился, пока лежал в очереди... Такую сущность создавать не нужно» — Intake проверяет живость.
4. **`BridgeDisconnectSystem`** — читает асинхронные события отключения из роутера (`GetDisconnected`), помечает сущности `BridgeClientDisconnected`.

Такой порядок гарантирует: transfer проверяется до intake (устоявшиеся сущности уходят дальше, прежде чем принимать новых), cleanup — до intake (мусор не мешает), disconnect — последним (корректное доотмечение упавших). Прикладные системы слоя (`GuestScreeningSystem`, `PacketDispatchSystem`, `UserEnterSystem`, `SessionPacketRouterSystem`) выполняются **после** этих четырёх в том же тике.

## Политика — `BridgeHandoffPolicy`

`BridgeHandoffPolicy` — `public abstract class`: «политика трансфера, реализуется каждым слоем. Слой сам определяет внутренние условия готовности (например, пройден ли логин, загружен ли профиль)». Метод `TryTransfer(ProtoEntity, NetworkChannel, out BridgeHandoffData) → bool` + виртуальный `Init(IProtoSystems systems)` (кэширует аспекты).

Конкретные реализации:

- **`GatewayHandoffPolicy`** (Layer.Gateway) — передаёт игрока в Realm только когда есть `UserProfile`+`NetworkSession` и `flowState.BundleIndex >= 6` (Configuration завершена). Упаковывает `EnterRealmHandoffData(profile, session)`. См. [Gateway](../gateway/index.md).
- **`RealmNetworkHandoffPolicy`** (Layer.Realm) — no-op: `TryTransfer` всегда `false`. Realm терминальный, никому игроков не передаёт.

Global — корневой слой, у него `GetHandoffPolicy()` возвращает `null` и `BridgeLayerModule` не подключается (в Global нет игроков).

## Жизненный цикл игрока через Bridge

Полный путь одного соединения от accept'а до фазы Play:

```
accept-поток (TcpNetworkService):
    socket → NetworkChannel
    router.HandleConnect(channel)         # ownership = GATEWAY, channel в _pending

ECS-тик, GATEWAY-мир, Bridge-системы:
    BridgeIntakeSystem:     channel из _pending → сущность (Pending+Connected) + entity↔channel
    (BridgeTransfer/Cleanup/Disconnect — нет условий)
    GuestScreeningSystem:   TryDequeueHandoff → Handshake → PromoteToSession (NetworkSession+FlowState)
    PacketDispatchSystem:   SequentialPacketPipeline гоняет фазы Status/Login/Configuration
        ...по достижении BundleIndex 6...
    BridgeTransferSystem (след. тик): GatewayHandoffPolicy.TryTransfer → true
        → router.TransferToNext: ownership = REALM, PendingTransfer в очередь REALM
        → сущность удалена из GATEWAY, сокет жив

ECS-тик, REALM-мир, Bridge-системы:
    BridgeIntakeSystem:     PendingTransfer → сущность (Pending+Connected) + entity↔channel
    (BridgeTransfer — nextScope пуст, no-op)
    UserEnterSystem:        TryDequeueHandoff → EnterRealmHandoffData → фаза Play (чанки, позиция)
    SessionPacketRouterSystem: DispatchPacketPipeline по play-пакетам
```

На каждом переходе между мирами ECS-сущность **пересоздаётся** (у каждого слоя свой `ProtoWorld`), но `NetworkChannel` и его очереди — один и тот же объект; ownership просто перетекает от скоупа к скоупу. Данные игрока переносятся в DTO (`BridgeHandoffData`), а не в компонентах — компоненты предыдущего мира уничтожаются вместе с сущностью.
