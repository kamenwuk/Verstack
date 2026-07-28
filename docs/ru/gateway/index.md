# Слой Gateway

Gateway — входной слой сервера, GATEWAY-мир в ECS. Обрабатывает всё, что происходит до входа игрока в игровой мир: Handshake (развод по фазам), Status (пинг сервер-листа, MOTD), Login, Configuration. Полностью построен на ECS: каждая фаза — это системы над миром, а пакеты идут через конвейер из `PacketBundle`'ов.

Сборку мира делает `GatewayFeature : VerstackFeature` — регистрирует аспект (`GatewayCacheStore`), системы (`GuestScreeningSystem`, `PacketDispatchSystem`) и сервис `GatewayPacketPipeline`, затем инициализирует конвейер. Feature подключается в `ServerComposer` вместе с `GlobalFeature` и `RealmFeature`. GATEWAY-мир видит GLOBAL (см. [Архитектуру](../architecture.md) — направление видимости миров).

## Аспект и side-данные

**`GatewayCacheStore : ProtoAspectInject`** — аспект мира. Содержит три пула компонентов:

- `ProtoPool<NetworkSession> Sessions` — сессия игрока: protocolVersion, IP, serverAddress, serverPort (struct).
- `ProtoPool<PacketFlowState> FlowStates` — где сущность в конвейере бандлов (`BundleIndex`/`StepIndex`).
- `ProtoPool<UserProfile> UserProfiles` — профиль игрока, заполняется в Login: `Uuid` + `Username`. Хранится для следующих фаз (Configuration/Play).

Помимо пулов, аспект держит side-данные: два словаря `entity ↔ NetworkChannel`. Прямой (`int → NetworkChannel`) нужен системам, чтобы по сущности достать канал. Обратный (`NetworkChannel → int`) — только для обработки дисконнекта: по мёртвому каналу найти сущность и удалить её из мира. `NetworkChannel` — sealed class с `PipeReader`/`PipeWriter`, в `struct`-компонент его не положить, поэтому связь хранится в аспекте, а не в пуле.

## Системы

**`GuestScreeningSystem : IProtoRunSystem`** — гости и решение по Handshake. В `Run()`:

1. Вычитывает `DisconnectedChannels` из `TcpNetworkService`, убирает мёртвые каналы из своих списков и, если канал был уже в ECS (Status или Login), удаляет сущность через `RemoveChannel` + `_world.DelEntity`.
2. Вычитывает `PendingConnections` в `_awaitingHandshake` (внутренний список).
3. Для каждого ожидающего канала парсит Handshake через `GatewayIntakeHandler.TryParseHandshake`. Результат разводит: `-1` — кик, `1` (Status) — `PromoteToSession(..., bundleIndex: 0)`, `2` (Login) — `PromoteToSession(..., bundleIndex: 2)`. Оба варианта создают ECS-сущность; различаются только стартовой позицией в конвейере.

`PromoteToSession` создаёт сущность: `Sessions.NewEntity` возвращает `ref` на слот, туда пишется `NetworkSession` с данными из handshake, добавляется `PacketFlowState` с заданным `BundleIndex`, регистрируется связь в `GatewayCacheStore`. Дальше канал обрабатывает `PacketDispatchSystem`.

**`PacketDispatchSystem : IProtoRunSystem`** — бандловая фаза. Для каждой сущности в `Sessions` берёт канал и `FlowState`, арендует два буфера из `ArrayPool` на тик и строит `PacketOutbound`. Вычитывает `IncomingPackets` и гонит каждый через `GatewayPacketPipeline.TryProcessPacket`. Ответ бандла копится в framing-буфере `PacketOutbound` и флашится в канал одним куском после опустошения очереди. Если бандл вернул `false` или если `BundleIndex` вышел за пределы конвейера (все фазы пройдены) — канал отключается: сначала flush, потом disconnect, чтобы send-воркер всегда писал в живой `PipeWriter`.

## Конвейер бандлов

**`GatewayPacketPipeline : IProtoInitService`** — сервис-обёртка над `PacketPipeline`, инжектится `[DI]` в `PacketDispatchSystem`. `Init` собирает упорядоченный массив бандлов; `TryProcessPacket(entity, packet, ref outbound, ref state)` делегирует текущему бандлу по `state.BundleIndex`. `BundleCount` отдаёт длину массива — диспетчер по нему определяет пройденный конвейер.

Конвейер (Status и Login оба на сущности, различаются стартовым `BundleIndex`):

| Индекс | Бандл | Входящий (шаг) | Ответ | Дальше |
|---|---|---|---|---|
| 0 | `StatusExchangeBundle` | Status Request (0x00) | Status Response (JSON из `ServerInfoCacheStore`) | 1 |
| 1 | `PingPongBundle` | Ping Request (0x01) | Pong Response (эхо long timestamp) | 2 |
| 2 | `LoginStartBundle` | Login Start (0x00) | Set Compression (0x03) + Login Success (0x02) | 3 |
| 3 | `LoginAcknowledgedBundle` | Login Acknowledged (0x03) | — | за пределы → disconnect |

Status стартует с 0, Login — с 2; оба после `PromoteToSession` крутятся в `PacketDispatchSystem`. Каждый бандл stateless; per-connection состояние лежит в ECS-компонентах на сущности (`NetworkSession`, `PacketFlowState`, `UserProfile`), а бандл читает/пишет их через `ProtoEntity`, который получает.

### Offline-флоу Login

`LoginStartBundle` читает `Name` и клиентский `Player UUID` (последний игнорируется — offline-режим генерирует свой). Считает `Uuid.GenerateOfflinePlayer(name)`, пишет `UserProfile` на сущность через `GetOrAdd`, затем отправляет `Set Compression` (несжатый — compression на канале ещё не включена), потом `EnableCompression(threshold)` и затем `Login Success` (уже в compressed framing). `Login Success` несёт, по протоколу 776: `UUID` игрока, `Username`, пустой массив `Properties` и `Session ID` UUID (свежий `Guid.NewGuid()`).

`LoginAcknowledgedBundle` подтверждает получение Login Success. После него `BundleIndex` выходит за пределы конвейера и `PacketDispatchSystem` закрывает канал — Configuration/Play пока не реализованы.

## Handler

`GatewayIntakeHandler` — stateless-помощник для `GuestScreeningSystem`. `TryParseHandshake(packet, out (protocolVersion, serverAddress, serverPort) data)` парсит Handshake-пакет (0x00): protocolVersion, serverAddress, serverPort, nextState. Возвращает `1` (Status), `2` (Login) или `-1` (невалиден). При `1`/`2` возвращает распарсенные поля через `out`-кортеж; `PromoteToSession` записывает их в `NetworkSession`.

## Текущие ограничения

- Configuration не реализован: слой пока не обрабатывает пакеты после Login Acknowledged, поэтому канал закрывается при завершении фазы.
- Размеры scratch-буферов `ArrayPool` в `PacketDispatchSystem` (`FRAME_SCRATCH_SIZE = 16 КБ`, `PAYLOAD_BUFFER_SIZE = 4 КБ`) покрывают Status/Login с запасом, но маловаты для чанков фазы Play — там понадобится динамический размер или flush-на-пакет.
