# Layers

Фазовые слои Minecraft на ECS. Каждый слой — это `ServerFeatureLayer` (точка расширения движка) с собственным ECS-миром,
своими системами и кэш-сторами. Слои видят только [Global](global.md); связь между Gateway и Realm — только через
[Bridge](../engine/bridge.md).

```text
Verstack.Layers.Global   ← виден всем, сам никого не знает      → global.md
Verstack.Layers.Gateway  ← Status / Login / Configuration       → gateway.md
Verstack.Layers.Realm    ← фаза Play (Join, Movement)           → realm.md
```

## ServerFeatureLayer

Абстрактный класс из `Verstack.Engine.Lifecycle` — точка входа слоя. Через его методы `ServerComposer` собирает мир
(см. [engine/index.md](../engine/index.md#serverfeaturelayer)). Что реализует автор слоя:

| Член | Назначение |
|------|------------|
| `Scope` | Имя мира слоя (`GLOBAL`/`GATEWAY`/`REALM`) |
| `GetCacheStores()` | Аспекты слоя — `ProtoAspectInject[]` (пулы компонентов, фильтры) |
| `GetVisibleScopes(...)` | Чужие миры, которые слой хочет видеть (у Gateway и Realm — только `GLOBAL`) |
| `GetNextScope()` | Скоуп следующего слоя для хэндоффа (у Gateway — `REALM`, у Global и Realm — пусто) |
| `GetHandoffPolicy()` | `BridgeHandoffPolicy` для передачи дальше (у Gateway — есть, у Global и Realm — `null`) |
| `Init(systems)` | Регистрация игровых систем (после сборки мира и видимости) |

Конкретные реализации — [`GlobalLayer`](global.md), [`GatewayLayer`](gateway.md), [`RealmLayer`](realm.md).

## Bundle-конвейер

Каждая Minecraft-фаза — это набор `PacketBundle`'ов, прогоняемых через конвейер. Бандл описывает исходящие пакеты через
`PacketOutbound` (см. [engine/network.md](../engine/network.md#читатели-и-писатели)); фрейминг и компрессия — забота
транспорта. Бандлы и конвейеры живут в `Engine.Network.Packet.Pipeline` (базовый механизм), фазовые бандлы — внутри
каждого слоя.

**Два конвейера под разные семантики:**

| Конвейер | Состояние | Для чего |
|----------|-----------|----------|
| `SequentialPacketPipeline` | stateful (`PacketFlowState`) | Жёсткая последовательность шагов: Handshake→Login→Configuration, Join. Двигается строго вперёд |
| `DispatchPacketPipeline` | stateless | Произвольный порядок: пакеты Play (movement) маршрутизируются по ID за O(1) |

### PacketBundle

Абстрактный класс. Каждый бандл — это сценарий из `StepCount` шагов. `TryProcess(stepIndex, entity, in packet, ref outbound)`
обрабатывает пакет на текущем шаге и возвращает `PacketHandleResult`:

| Результат | Что делает конвейер |
|-----------|---------------------|
| `Accepted` | Шаг пройден. `StepIndex++`; при исчерпании `StepCount` — `BundleIndex++` (только Sequential) |
| `Ignored` | Пакет легитимный, но не триггер текущего шага (напр. `minecraft:brand` в Configuration). Проглатывается без продвижения |
| `Continue` | Перепроверить тот же пакет на следующем шаге (loop внутри `ProcessSession`) |
| `Kick` | Пакет невалиден — клиент отключается |

### PacketFlowState

`struct(BundleIndex, StepIndex)` — позиция в линейном конвейере. Хранится в кэш-сторе слоя по одной на сущность игрока.
Инициализируется при переводе игрока на «рельсы» (напр. в `GuestScreeningSystem` — с `bundleIndex` Status или Login).

## Intake: от сокета до бандла

Игрок попадает в слой через Bridge. Цикл жизни на тике слоя (после Bridge-систем Transfer→Cleanup→Intake→Disconnect):

1. **Приём.** Игровая система вычитывает новых игроков через `TryDequeueHandoff` (сущность уже в `Connected`).
2. **Создание сессии.** Первый пакет разбирается вручную (Handshake — в `GatewayIntakeHandler`), по нему создаётся
   `NetworkSession` и инициализируется `PacketFlowState` с нужным `BundleIndex`.
3. **Диспетч.** По `ConnectedFilter` идёт основная система: для каждой активной сущности `pipeline.ProcessSession(...)`.
4. **Хэндофф.** Когда Sequential-конвейер доходит до конца массива бандлов, возвращается `PipelineSessionStatus.Transfer`.
   Сам слой игрока не переносит — этим занимается `BridgeHandoffPolicy` на следующем тике (в `BridgeTransferSystem`).
5. **Кик.** `PipelineSessionStatus.Kick` → `channel.Disconnect()`. Сеть сообщит роутеру, `BridgeDisconnectSystem` повесит
   `BridgeClientDisconnected`, `BridgeCleanupSystem` снесёт сущность.

## CacheStore

Каждый слой держит свои пулы компонентов и фильтры в `ProtoAspectInject` (наследник) — `GatewayCacheStore`,
`UserSessionCacheStore` (Realm), `ServerInfoCacheStore`/`SyncedRegistryCatalog` (Global). Доступ к пулам — через `[DI]`,
инъекцию делает `AutoInjectModule`. Фильтры — `ProtoIt`/`ProtoItExc`, на них крутятся системы.

Игрок в слое идентифицируется парой «сущность + `NetworkChannel`». Маппинг entity↔channel держит `BridgeStateCacheStore`
(`GetChannel(entity)`); фазовые данные (session, profile, flowState) лежат в кэш-сторе слоя по той же сущности.
