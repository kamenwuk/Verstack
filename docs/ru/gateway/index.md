# Слой Gateway

Gateway — входной слой сервера, GATEWAY-мир в ECS. Обрабатывает всё, что происходит до входа игрока в игровой мир: Handshake (развод по фазам), Status (пинг сервер-листа, MOTD), Login, Configuration. Полностью построен на ECS: каждая фаза — это системы над миром, а пакеты идут через конвейер из `PacketBundle`'ов.

Сборку мира делает `GatewayLayer : ServerFeatureLayer` (см. [Lifecycle](../architecture.md) про базовый класс) — регистрирует аспект `GatewayCacheStore` и системы `GuestScreeningSystem` + `PacketDispatchSystem`. Видит миры `GLOBAL` и `REALM`; следующий слой в цепочке handoff — `REALM`; политика передачи — `GatewayHandoffPolicy`. Через `BridgeLayerModule` первым делом подключаются четыре Bridge-системы (Transfer/Cleanup/Intake/Disconnect, см. [Bridge](../bridge/index.md)), затем — фазовые. Слой подключается в `EntryPoint.Start` вместе с Global и Realm.

## Аспект

**`GatewayCacheStore : ProtoAspectInject`** — аспект мира. Содержит три пула компонентов и один фильтр:

- `ProtoPool<NetworkSession> Sessions` — сессия игрока: protocolVersion, IP, serverAddress, serverPort (struct из `Verstack.Layer.Global.User`).
- `ProtoPool<UserProfile> UserProfiles` — профиль игрока, заполняется поэтапно: `Uuid` + `Username` в Login, `Locale` — в Configuration из Client Information. Хранится для передачи в Realm.
- `ProtoPool<PacketFlowState> FlowStates` — где сущность в конвейере бандлов (`BundleIndex`/`StepIndex`; тип из `Verstack.Network.Packet.Pipeline`).
- `ActiveSessionsFilter` — `ProtoIt` по `(NetworkSession, PacketFlowState)`: активные сессии, которые гоняет `PacketDispatchSystem`.

Связь entity↔channel в этом аспекте **не** хранится — она в `BridgeStateCacheStore` (общий аспект Bridge на каждый слой). Системы достают канал через `_bridgeStateCacheStore.GetChannel(entity)`, а фильтр активных сущностей — `ConnectedFilter` оттуда же.

## Системы

**`GuestScreeningSystem : IProtoRunSystem`** — гости и решение по Handshake. В `Run()`:

1. Вычитывает handoff из `BridgeStateCacheStore.TryDequeueHandoff`. В `payload.Entity` уже вшит `BridgeClientConnected` (игрок «на рельсах»); `payload.Data` для Gateway — `null` (это первый слой, nobody передаёт данные в Gateway).
2. Достаёт `channel` через `GetChannel(entity)` и в цикле вычитывает `IncomingPackets`, пока состояние не изменится. Для каждого пакета парсит Handshake через `GatewayIntakeHandler.TryParseHandshake`. Результат разводит: `-1` — кик (`channel.Disconnect()`), `1` (Status) — `PromoteToSession(entity, channel, data, bundleIndex: 0)`, `2` (Login) — `PromoteToSession(..., bundleIndex: 2)`.

`PromoteToSession` добавляет на сущность `NetworkSession` (с данными из handshake) и `PacketFlowState` с заданным `BundleIndex`, `StepIndex = 0`. После `PromoteToSession` дальнейшие пакеты канала обрабатывает `PacketDispatchSystem`.

**`PacketDispatchSystem : IProtoInitSystem, IProtoRunSystem`** — бандловая фаза. В `Init` строит `SequentialPacketPipeline` (из `Verstack.Network.Packet.Pipeline`) с упорядоченным массивом из 7 бандлов. В `Run` итерируется по `ConnectedFilter` из `BridgeStateCacheStore`, пропускает сущности без `ActiveSessionsFilter` (ещё не промоутнутые GuestScreening'ом), берёт канал и `ref FlowState`, вызывает `_pipeline.ProcessSession(entity, channel, ref flowState)`:

- `Transfer` — конвейер пройден до конца (все фазы завершены). **Gateway ничего не делает:** `GatewayHandoffPolicy` в Bridge-системе трансфера увидит `BundleIndex >= 6` и сама перенесёт игрока в Realm, не закрывая сокет.
- `Kick` — нарушение протокола. `channel.Disconnect()`; Bridge сообщит роутеру, сущность удалится в Bridge-системе чистки.

## Конвейер бандлов

`SequentialPacketPipeline` держит фиксированный порядок бандлов (индекс = `BundleIndex` в `PacketFlowState`):

| Индекс | Бандл | Входящий (шаг) | Ответ | Дальше |
|---|---|---|---|---|
| 0 | `StatusExchangeBundle` | Status Request (0x00) | Status Response (JSON из `ServerInfoCacheStore`) | 1 |
| 1 | `PingPongBundle` | Ping Request (0x01) | Pong Response (эхо long timestamp) | 2 |
| 2 | `LoginStartBundle` | Login Start (0x00) | Set Compression (0x03) + Login Success (0x02) | 3 |
| 3 | `LoginAcknowledgedBundle` | Login Acknowledged (0x03) | — | 4 |
| 4 | `ClientInformationBundle` | Client Information (0x00) | Known Packs (0x0E): `minecraft:core@26.2` | 5 |
| 5 | `KnownPacksBundle` | Known Packs response (0x07) | Registry Data (0x07) × 29 → Update Tags (0x08) → Feature Flags (0x0C) + Finish Configuration (0x03) | 6 |
| 6 | `ConfigurationFinishBundle` | Acknowledge Finish (0x03) | — | за пределы → `Transfer` |

Status стартует с `BundleIndex=0`, Login — с `BundleIndex=2`; Configuration продолжается с 4 после Login Acknowledged. Все бандлы stateless; per-connection состояние лежит в ECS-компонентах на сущности (`NetworkSession`, `PacketFlowState`, `UserProfile`), а бандл читает/пишет их через `ProtoEntity`. Каждый бандл возвращает `PacketHandleResult`: `Accepted` (двигаем шаг), `Ignored` (проглотить без продвижения), `Kick` (разрыв), `Continue` (многошаговая отправка, только в `KnownPacksBundle`).

### Offline-флоу Login

`LoginStartBundle` читает `Name` и клиентский `Player UUID` (последний игнорируется — offline-режим генерирует свой). Считает `Uuid.GenerateOfflinePlayer(name)` (extension из Network Writers), пишет `UserProfile` на сущность через кэш-стор, затем отправляет `Set Compression` (несжатый — compression на канале ещё не включена), потом `outbound.EnableCompression(threshold)` и затем `Login Success` (уже в compressed framing). `Login Success` несёт, по протоколу 776: `UUID` игрока, `Username`, пустой массив `Properties` и `Session ID` UUID (свежий `Guid.NewGuid()`).

`LoginAcknowledgedBundle` подтверждает получение Login Success. После него клиент переходит в состояние Configuration, и конвейер продолжается бандлом `ClientInformationBundle`.

### Configuration flow

Configuration — фаза после Login Acknowledged, доводит клиента до готовности к Play. Реализована тремя реактивными бандлами (ждём триггерный пакет клиента → отвечаем серверным):

- `ClientInformationBundle` (0x00 → 0x0E). Читает `locale` из Client Information и сохраняет его в `UserProfile` (понадобится в Play). Отправляет S→C Known Packs с одним паком `minecraft:core@26.2` — сервер блокирует Configuration до получения ответа клиента.
- `KnownPacksBundle` (0x07 → 0x07 × 29 → 0x08 → 0x0C + 0x03). Три шага: (0) читает подмножество паков, известных клиенту, затем шлёт по одному Registry Data на каждый synced-реестр 26.2 (29 шт., listing-only, через `SyncedRegistryCatalog` из Layer.Global — с NBT-телами для `minecraft:overworld` DimensionType и `minecraft:plains` Biome, собираемыми через `NbtWriter` из `stackalloc`-буферов); (1) шлёт Update Tags пакетно через `AssetSource.GetTagBatch()` (теги предзагружены из `assets/` в `Verstack.Shared.Assets`); (2) Feature Flags (`["minecraft:vanilla"]`) + Finish Configuration.
- `ConfigurationFinishBundle` (0x03 → —). На Acknowledge Finish Configuration логирует username. Конвейер доходит до конца массива → pipeline возвращает `Transfer` → `GatewayHandoffPolicy` передаёт игрока в Realm.

### Registry Data (listing-only)

Registry Data (S→C 0x07) отправляется 29 раз — по одному packet на synced-реестр 26.2. Список реестров и обязательные entry-ids хранятся в Layer.Global (`SyncedRegistryCatalog`, см. [Global](../global/index.md)). Wire-формат 26.2 — framed stream-codec: `[packet-id][Identifier реестра][VarInt count][entries]`, без корневого NBT-Compound (это формат ≤1.20.x). Для 13 обязательных реестров (variant-реестры + `painting_variant`) посылаются canonical entry-ids **без тел** — каждый entry это `Identifier + TAG_End (0x00)`, то есть `Optional<Tag> = empty`; клиент берёт тела из bundled-datapack. Остальные 16 уходят пустыми (`count=0`). Для `dimension_type` и `worldgen/biome` отправляются полноценные NBT-тела (нужны клиенту сразу). Listing-only принят клиентом 26.2.

Update Tags (S→C 0x08) отправляются одним пакетом, тело собирается из предзагруженного `AssetSource.GetTagBatch()` (все `*.tags` файлы из `assets/`).

Посторонние пакеты, которые клиент шлёт проактивно в Configuration (напр. `minecraft:brand`, C→S 0x02), возвращаются бандлами как `Ignored` — конвейер проглатывает их без кика и без продвижения.

## Handoff в Realm

Когда `PacketDispatchSystem` для сущности получает `Transfer` (все 7 бандлов пройдены), он просто `continue`. Дальше работает `GatewayHandoffPolicy` (вызывается Bridge-системой трансфера в том же тике):

```csharp
// GatewayHandoffPolicy.TryTransfer
if (!_gatewayCache.UserProfiles.Has(entity) || !_gatewayCache.Sessions.Has(entity)) return false;
if (flowState.BundleIndex < 6) return false;   // конфигурация ещё не завершена
data = new EnterRealmHandoffData(profile, session);   // DTO из Layer.Global
return true;
```

`BridgeTransferSystem` вызывает `TransferToNext` на роутере: сущность удаляется из GATEWAY-мира (сокет **не** закрывается), и `EnterRealmHandoffData` попадает в handoff-очередь REALM-мира. Там её вычитает `UserEnterSystem` (см. [Realm](../realm/index.md)).

## Handler

`GatewayIntakeHandler` — `internal static class`, stateless-помощник для `GuestScreeningSystem`. `TryParseHandshake(packet, out (protocolVersion, serverAddress, serverPort) data)` парсит Handshake-пакет (0x00): protocolVersion, serverAddress, serverPort, nextState. Возвращает `1` (Status), `2` (Login) или `-1` (невалиден). При `1`/`2` возвращает распарсенные поля через `out`-кортеж; `PromoteToSession` записывает их в `NetworkSession`.
