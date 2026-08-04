# Bridge

Развязка асинхронного сетевого слоя и синхронного ECS-тика. Это единственный путь, которым подключение игрока
переходит из одного слоя в другой: Gateway и Realm [друг друга не видят](../architecture.md#миры-и-видимость), а Bridge
передаёт владение каналом между ними.

Bridge стоит между `Engine.Network` и фазовыми слоями. Сеть качает байты в фоновых потоках; ECS крутится в одном потоке
тик-лупа и не потокобезопасен. Bridge стыкует эти два мира через очереди и конечный автомат игрока.

## Состав

Все типы в проекте `Verstack.Engine.Bridge`. Четыре системы — `internal sealed`, остальное — публичный контракт для слоёв.

**Роутер и модуль (один экземпляр на сервер):**

| Тип | Роль |
|-----|------|
| `BridgeHandoffRouter` | Центральный хаб. Владеет ownership'ом каналов, развязывает потоки и ECS через `ConcurrentQueue`. Реализует `ClientLifecycleHandler` (см. [engine/index.md](index.md#точка-стыка-network--bridge)) |
| `BridgeLayerModule` | Интеграция Bridge в конкретный ECS-слой. `IProtoModule`: ставит четыре системы в фиксированном порядке и регистрирует аспект `BridgeStateCacheStore`. **Не инъектируется в Global** |

**Аспект (экземпляр на слой):**

| Тип | Роль |
|-----|------|
| `BridgeStateCacheStore` | `ProtoAspectInject` (CacheStore). Конечный автомат игрока, фильтры, пулы компонентов, маппинги entity↔channel, FIFO-очередь хэндоффов. Работает только над сущностями своего мира |

**Компоненты (ECS-маркеры состояния):**

| Компонент | Значение |
|-----------|----------|
| `BridgeHandoffPending` | Игрок создан в ECS, но ждёт инициализации специфичными системами слоя |
| `BridgeClientConnected` | Игрок на «рельсах» — активен, виден игровым системам |
| `BridgeClientDisconnected` | Соединение потеряно; ждёт очистки |

**Контракт данных между слоями:**

| Тип | Роль |
|-----|------|
| `BridgeHandoffData` | Абстрактный `record`-маркер для DTO. Наследники (records) лежат в общих контрактах и содержат данные, необходимые принимающему слою для инициализации игрока |
| `HandoffPayload` | `readonly struct`, выдаётся из `TryDequeueHandoff`: сущность + DTO |
| `BridgeHandoffPolicy` | Абстрактная политика трансфера. Реализуется слоем, который передаёт игроков дальше; решает, готов ли игрок к переходу |

## Конечный автомат игрока

Сущность игрока в Bridge проходит состояния через три маркера:

```text
Pending → Connected → (Disconnected | Handoff)
   │         │              │
   │         │              └─ BridgeTransferSystem: уходит в следующий слой (сокет жив)
   │         │
   │         └─ игровая система слоя: TryDequeueHandoff
   │
   └─ BridgeIntakeSystem: создаёт из очереди роутера
```

- **Pending.** Сущность создана Bridge'ом, ждёт, пока игровая система слоя не заберёт её из очереди. Игровым системам
  **не видна** — у них нет `BridgeClientConnected`.
- **Connected.** Игровая система забрала игрока (`TryDequeueHandoff`), компонент `BridgeHandoffPending` снят, повешен
  `BridgeClientConnected`. Теперь сущность видна в `ConnectedFilter` — на ней работает фазовая логика слоя.
- **Disconnected.** TCP-поток сообщил об обрыве; `BridgeDisconnectSystem` повесил маркер. Сущность доживает до ближайшего
  `BridgeCleanupSystem`.

Слой, который не передаёт игроков дальше (Realm), не имеет `BridgeHandoffPolicy` — его игроки живут в Connected до
дисконнекта.

## Пайплайн тика

`BridgeLayerModule.Init` регистрирует четыре системы в строгом порядке. На каждом тике слоя они отрабатывают
последовательно:

| # | Система | Что делает |
|---|---------|------------|
| 1 | `BridgeTransferSystem` | Для каждого `ConnectedFilter`: если `handoffPolicy.TryTransfer` одобрил — `router.TransferToNext` (ownership → next scope, в очередь next), `Remove(e, closeSocket:false)`. Сокет **не закрывается** — он перешёл во владение следующего слоя. Тупиковый слой (`nextScope` пуст) — ранний выход |
| 2 | `BridgeCleanupSystem` | Снимает мусор (`PendingGarbageFilter`) и отвалившихся (`DisconnectedFilter`). `Remove(e, closeSocket:true)` — сокет **закрывается** |
| 3 | `BridgeIntakeSystem` | `router.TryDequeuePending` → `RegisterPending(channel, data)`. Защита от гонки: если канал уже `IsDisconnected`, пропускает |
| 4 | `BridgeDisconnectSystem` | `router.GetDisconnected(scope)` → `MarkDisconnected` (вешает `BridgeClientDisconnected`) |

Порядок не случаен: Transfer уходит **до** игровых систем, чтобы переданные игроки не отработали лишний тик в уходящем
слое; Cleanup идёт **до** Intake, чтобы в текущем тике вновь прибывшие не смешались с удаляемыми; Disconnect ставит
маркеры **в конце** тика — они отработают в Cleanup следующего.

После Bridge-систем идут игровые системы слоя (`GatewayIntakeHandler`, `PacketDispatchSystem` и т.д.) — они работают с
`ConnectedFilter` и `TryDequeueHandoff`.

## Развязка потоков

`BridgeHandoffRouter` — единственный объект, к которому прикасаются оба потока. TCP-поток (`TcpNetworkService`) пишет,
ECS-тик читает:

- `HandleConnect(channel)` (TCP-поток): `ownership[channel] = defaultScope` (обычно GATEWAY), `pending[defaultScope].Enqueue`.
- `HandleDisconnect(channel)` (TCP-поток): смотрит текущий owner, `disconnected[owner].Enqueue`.
- `TransferToNext(currentScope, channel, data)` (ECS-тик, из Transfer-системы): под `Lock` проверяет, что текущий owner
  — это `currentScope`, перекладывает ownership на `nextScope`, `pending[nextScope].Enqueue`.

Доступ к `_ownership` защищён `Lock`; очереди `ConcurrentQueue<PendingTransfer>` / `ConcurrentQueue<NetworkChannel>`
разделяются по скоупу (`Dictionary<string, ConcurrentQueue<...>>`), по одной паре на слой. Так TCP-поток пишет в
«чужой» скоуп только при connect/disconnect, а ECS-тик каждого слоя читает только свою очередь.

## Контракт для игрового слоя

Игровая система слоя получает игрока так:

```csharp
[DI] private readonly BridgeStateCacheStore _state = null!;

// в Run():
while (_state.TryDequeueHandoff(out var payload))
{
    // payload.Entity — сущность в состоянии Connected
    // payload.Data   — BridgeHandoffData от предыдущего слоя (или null при первичном входе)
    // дальше: навесить фазовые компоненты, начать сценарий (логин, join и т.д.)
}
```

`TryDequeueHandoff` переводит сущность из Pending в Connected (вешает `BridgeClientConnected`, снимает
`BridgeHandoffPending`) и возвращает `HandoffPayload`. Если игрок отключился, пока висел в очереди, — пропускается
(будет снят как мусор). Фильтр `ConnectedFilter` — это `It.Inc<BridgeClientConnected>().Exc<BridgeClientDisconnected>()`:
на нём крутятся все фазовые системы обработки пакетов.

Для передачи игрока дальше слой реализует `BridgeHandoffPolicy.TryTransfer(entity, channel, out data)` — возвращает
`true`, когда внутреннее условие выполнено (логин пройден, профиль загружен), и собирает DTO `data` для следующего слоя.
