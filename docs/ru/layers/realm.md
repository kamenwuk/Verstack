# Realm

REALM-мир — фаза Play. Принимает игрока из [Gateway](gateway.md) через [Bridge](../engine/bridge.md), прогоняет
сценарий входа в игру (Join) и дальше обрабатывает ввод игрока (Movement). Тупиковый слой: не передаёт игрока дальше
(`GetNextScope` — пусто, `GetHandoffPolicy` — `null`), игрок живёт здесь до дисконнекта.

Реализация — `RealmLayer : ServerFeatureLayer` (`Scope = REALM`): видит `GLOBAL`, две игровые системы —
`HandoffApprovalSystem` (приём хэндоффа + Join-конвейер) и `InboundDispatcherSystem` (ввод). Кэш-стор —
`UserSessionCacheStore`.

## Приём хэндоффа и Join

`HandoffApprovalSystem` работает в две фазы на каждом тике:

**Фаза 1 — одобрение хэндоффа.** Вычитывает новых игроков через `_bridgeStateCacheStore.TryDequeueHandoff`. Если
`payload.Data` — `EnterRealmHandoffData`, сеет в `UserSessionCacheStore` по сущности: `UserProfile`, `NetworkSession`,
`PacketFlowState(0, 0)`. После этого сущность готова к Join-сценарию.

**Фаза 2 — Join-конвейер.** По `ConnectedFilter` прогоняет `SequentialPacketPipeline` из 6 бандлов. Состояние —
`PacketFlowState` (в `UserSessionCacheStore`).

```text
[0] JoinLoginBundle         ждёт Login Ack 0x03  → Login (Play) 0x31 (game mode, dimension, seed, sea level)
[1] JoinSpawnPointBundle    (без триггера)        → set_default_spawn_position 0x61
[2] JoinTabListBundle       (без триггера)        → tab list
[3] JoinCommandCatalogBundle (без триггера)       → command catalog
[4] JoinChunkBatchBundle    (без триггера)        → chunk batch (готовность клиента к чанкам)
[5] JoinTeleportBundle      (без триггера)        → teleport игрока на спавн
```

Первый бандл (`JoinLoginBundle`) — единственный, что ждёт пакет клиента: `Login Acknowledged 0x03`. Прочие пакеты на
этом шаге — `Ignored`. Остальные 5 бандлов триггерятся движением конвейера (`Continue`), а не входящим пакетом —
сервер сам шлёт команды клиенту. `PipelineSessionStatus.Transfer` (конец массива) означает завершение Join —
игрок полностью в игре.

## Ввод игрока

После Join за ввод отвечает `InboundDispatcherSystem` + `DispatchPacketPipeline` (stateless, маршрутизация по ID пакета):

| Packet ID | Bundle | Назначение |
|-----------|--------|------------|
| `0x00` | `ConfirmTeleportBundle` | Подтверждение телепорта (clientbound teleport → ack) |
| `0x1E` | `SetPlayerPositionBundle` | Движение: позиция без поворота |
| `0x1F` | `SetPlayerPositionAndRotationBundle` | Движение: позиция + поворот |

Пакеты Play приходят в произвольном порядке — поэтому здесь Dispatch, а не Sequential. `Kick` → `channel.Disconnect()`,
дальше Bridge снимет сущность.

## UserSessionCacheStore

`ProtoAspectInject`: пулы `NetworkSession`, `UserProfile`, `PacketFlowState`. Те же типы, что и в
`GatewayCacheStore`, но экземпляры — свои, по той же сущности (она переезжает между слоями через Bridge). Фазовые
данные Join-бандлов берутся из `SpawnConstants`/`WorldConstants` (Global); константы — статические, без инъекции.

## Что дальше

Текущая область — Join + базовый movement. Симуляция мира (чанки, сущности, игровая логика) — не реализована и будет
дорабатываться поверх этой же структуры: системы над `ConnectedFilter`, обработка ввода через Dispatch-pipeline,
синхронизация состояния через исходящие пакеты.
