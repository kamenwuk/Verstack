# Слой Realm

Realm — REALM-мир в ECS, фаза Play. Здесь игрок попадает в игровой мир: получает Login(Play)-пакет, чанки, позицию и может слать пакеты движения. Вход в мир реализован; физика/движение и keep-alive пока заглушены.

Сборку мира делает `RealmLayer : ServerFeatureLayer` (см. [Lifecycle](../architecture.md) про базовый класс) — регистрирует аспект `UserSessionCacheStore` и системы `UserEnterSystem` + `SessionPacketRouterSystem`. Видит мир `GLOBAL`; следующий слой в цепочке handoff — пустая строка (Realm терминальный); политика передачи — `RealmNetworkHandoffPolicy` (no-op). Через `BridgeLayerModule` первым делом подключаются четыре Bridge-системы (см. [Bridge](../bridge/index.md)), затем — фазовые. Слой подключается в `EntryPoint.Start` в массиве слоев.

REALM тикает на 20 TPS вместе с остальными мирами в одном главном цикле (`EntryPoint.RunMainLoop`).

## Аспект

**`UserSessionCacheStore : ProtoAspectInject`** (`internal`) — аспект REALM-мира. Три пула:

- `ProtoPool<NetworkSession> Sessions` — сессия игрока (struct из Layer.Global): protocolVersion, IP, serverAddress, serverPort. Приходит из handoff.
- `ProtoPool<UserProfile> UserProfiles` — профиль игрока (struct из Layer.Global): uuid, username, locale. Приходит из handoff.
- `ProtoPool<PacketFlowState> FlowStates` — состояние прогресса по `SequentialPacketPipeline` входа (`BundleIndex`/`StepIndex`; тип из Network).

Связь entity↔channel — в `BridgeStateCacheStore` (общий аспект Bridge), как и в Gateway.

## Вход игрока — `UserEnterSystem`

`UserEnterSystem : IProtoInitSystem, IProtoRunSystem` — оркестратор перехода Configuration/Login → Play. DI: `BridgeStateCacheStore` (из Bridge), `UserSessionCacheStore` (свой аспект), `[DI(ServerWorldScopes.GLOBAL)] IPacketCompressor` (компрессор из GLOBAL).

В `Init` строит `SequentialPacketPipeline` с жёстко зашитым порядком из 7 бандлов (исходящие пакеты S→C стадии Play):

| Индекс | Бандл | Что отправляет |
|---|---|---|
| 0 | `PlayJoinBundle` | Clientbound Login (Play) `0x31`: entity id, hardcore=false, view/sim distance=10, измерение `minecraft:overworld`, game mode Creative |
| 1 | `PlaySpawnPositionBundle` | set_default_spawn_position `0x61`: `(8, 64, 8)` |
| 2 | `PlayInfoUpdateBundle` | player_info_update `0x46`: Add Player + Update Game Mode + Update Listed + Update Latency (из `UserSessionCacheStore.UserProfiles`) |
| 3 | `PlayCommandsBundle` | commands `0x10`: пустой граф (1 узел Root) — чтобы клиент не крашнулся и отключил Tab-завершение |
| 4 | `PlayGameEventBundle` | game_event `0x26`: event `13` (Start waiting for level chunks) — клиент выходит из экрана «Загрузка территории» |
| 5 | `PlayChunkBundle` | Set Center Chunk `0x5E` + Chunk Batch Start `0x0C` + level_chunk_with_light `0x2D` × 25 (сетка 5×5, через `FlatGenerator` из Engine.World) + Chunk Batch Finished `0x0B` |
| 6 | `PlayPositionBundle` | Synchronize Player Position `0x48`: teleport id 0, `(8, 80, 8)`, velocity 0, yaw/pitch 0, flags 0 |

`Run` делает две фазы:

1. **Потребление handoff-очереди.** `while (_bridgeStateCacheStore.TryDequeueHandoff(out var payload))` — если `payload.Data is EnterRealmHandoffData realmData`, добавляет на `payload.Entity` три компонента: `UserProfiles = realmData.Profile`, `Sessions = realmData.Session`, `FlowStates = new PacketFlowState(0, 0)`. Так игрок, переданный из Gateway, «приземляется» в REALM со своим профилем и сессией.
2. **Прогон pipeline по подключённым.** `foreach (entity in ConnectedFilter)` — берёт канал и `ref flowState`, вызывает `_pipeline.ProcessSession(entity, channel, ref flowState)`. `Transfer` (конвейер пройден) → `continue`; `Kick` (нарушение протокола) → `channel.Disconnect()`.

Каждый бандл — `sealed class : PacketBundle` (тип из Network), stateless; per-connection состояние в ECS-компонентах на сущности. `PlayInfoUpdateBundle` дополнительно переопределяет `Init`, чтобы закэшировать `UserSessionCacheStore`.

### Чанки

`PlayChunkBundle` отправляет чанки из `Verstack.Engine.World`: `FlatGenerator.Generate(x, z)` для сетки 5×5 (от -2 до 2 по X и Z), тело каждого чанка сериализуется через `Chunk.SerializeBody` (block states, heightmaps, полный свет — см. [Engine.World](../engine-world/index.md)). Center Chunk `(0, 0)`, batch size 25. Это тестовый flat-мир; замена на реальную генерацию — будущая задача.

## Маршрутизатор входящих — `SessionPacketRouterSystem`

`SessionPacketRouterSystem : IProtoInitSystem, IProtoRunSystem` — обрабатывает ВХОДЯЩИЕ пакеты стадии Play (C→S) от уже подключённых игроков. DI: `BridgeStateCacheStore`, `[DI(ServerWorldScopes.GLOBAL)] IPacketCompressor`. XML-doc: «запускается в самом начале тика, вычитывает все пакеты игроков и передает их в диспетчер».

В `Init` строит `DispatchPacketPipeline` (stateless-диспетчер по packet id, тип из Network) с таблицей из 3 бандлов:

| Packet ID (C→S) | Bundle | Что делает |
|---|---|---|
| `0x00` | `ConfirmTeleportBundle` | Читает `teleportId` (VarInt). TODO: снять флаг «Ожидание телепортации». |
| `0x1E` | `SetPlayerPositionBundle` | Читает X/Y/Z (double) + onGround (bool). TODO: `MoveRequestComponent`. |
| `0x1F` | `SetPlayerPositionAndRotationBundle` | Читает X/Y/Z (double) + yaw/pitch (float) + onGround (bool). TODO: `MoveRequestComponent` с yaw/pitch. |

Все бандлы в `Packets/Inbound/` (namespace `Verstack.Layer.Realm.Packets.Inbound`), каждый `sealed class : PacketBundle`, `StepCount = 1`. Каждый ожидает конкретный `packet.Id`, иначе `Ignored`; при невалидных данных (`!reader.IsValid`) → `Kick`.

В `Run` итерируется по `_bridgeStateCacheStore.ConnectedFilter`, для каждой сущности берёт `channel` через `GetChannel(entity)` и вызывает `_pipeline.ProcessSession(entity, channel)`. `Kick` → `channel.Disconnect()` + лог.

Физика/движение пока заглушены: бандлы только читают данные, TODO-компоненты (`MoveRequestComponent`) ещё не реализованы.

## Handoff-политика

`RealmNetworkHandoffPolicy : BridgeHandoffPolicy` — no-op: `Init` пустой, `TryTransfer` всегда возвращает `false`/`data = null`. Realm — терминальный слой, никому игроков не передаёт (соответствует `GetNextScope() => string.Empty`).

## Текущие ограничения

- **Физика/движение заглушены.** `SetPlayerPosition*Bundle` только читают координаты; TODO `MoveRequestComponent` для реальной обработки. Клиент может слать пакеты движения, но сервер их не применяет.
- **Keep-alive закомментирован.** `User/KeepAliveSystem.cs` целиком закомментирован (мёртвый код, не регистрируется в `RealmLayer`). `User/KeepAliveTimer.cs` — активный `internal readonly struct`, но потребителя сейчас нет. Реализация keep-alive — будущая задача.
- **Только flat-генератор.** Чанки идут из `FlatGenerator` (тестовый плоский мир). Реальная генерация/загрузка сохранений — через расширение `ChunkManager` (см. [Engine.World](../engine-world/index.md)).
- **Namespace-расхождение.** `Join/PlayChunkBundle.cs` объявлен в `Verstack.Layer.Gateway.Bundles` (артефакт переноса файла) — функционально работает, но namespace не совпадает с папкой.
