# Слой Gateway

Gateway — входной слой сервера, GATEWAY-мир в ECS. Обрабатывает всё, что происходит до входа игрока в игровой мир: Handshake (развод по фазам), Status (пинг сервер-листа, MOTD), Login, Configuration. Полностью построен на ECS: каждая фаза — это системы над миром, а пакеты идут через конвейер из `PacketBundle`'ов.

Сборку мира делает `GatewayFeature : VerstackFeature` — регистрирует аспект (`GatewayCacheStore`) и системы (`GuestScreeningSystem`, `PacketDispatchSystem`), плюс сервис `GatewayPacketPipeline`. Feature подключается в `ServerComposer` вместе с `GlobalFeature` и `RealmFeature`. GATEWAY-мир видит GLOBAL (см. [Архитектуру](../architecture.md) — направление видимости миров).

## Аспект и side-данные

**`GatewayCacheStore : ProtoAspectInject`** — аспект мира. Содержит два пула компонентов:

- `ProtoPool<NetworkSession> Sessions` — сессия игрока: protocolVersion, IP, serverAddress, serverPort (struct).
- `ProtoPool<PacketFlowState> FlowStates` — где сущность в конвейере бандлов (`BundleIndex`/`StepIndex`).

Помимо пулов, аспект держит side-данные: два словаря `entity ↔ NetworkChannel`. Прямой (`int → NetworkChannel`) нужен системам, чтобы по сущности достать канал для записи ответа. Обратный (`NetworkChannel → int`) — только для обработки дисконнекта: по мёртвому каналу найти сущность и удалить её из мира. `NetworkChannel` — sealed class с `PipeReader`/`PipeWriter`, в `struct`-компонент его не положить, поэтому связь хранится в аспекте, а не в пуле.

## Системы

**`GuestScreeningSystem : IProtoInitSystem, IProtoRunSystem`** — гости и Status. В `Run()`:

1. Вычитывает `DisconnectedChannels` из `TcpNetworkService`, убирает мёртвые каналы из своих списков и, если канал был уже в ECS (Login), удаляет сущность через `RemoveChannel` + `_world.DelEntity`.
2. Вычитывает `PendingConnections` в `_awaitingHandshake` (внутренний список).
3. Для каждого ожидающего канала парсит Handshake через `GatewayIntakeHandler.TryParseHandshake`. Результат разводит: `-1` — кик, `1` (Status) — переход в `_statusConnections`, `2` (Login) — создание ECS-сущности: `Sessions.NewEntity` возвращает `ref` на слот, туда пишется `NetworkSession` с данными из handshake + IP из канала, добавляется `PacketFlowState` (старт с `BundleIndex = 0`), регистрируется связь в `GatewayCacheStore`.
4. Для Status-каналов обслуживает пинг/MOTD напрямую через `GatewayIntakeHandler.TryHandleStatusRequest` — без создания ECS-сущности (Status — короткая безсессионная фаза).

**`PacketDispatchSystem : IProtoRunSystem`** — бандловая фаза. Идёт по всем сущностям в `Sessions`, для каждой берёт канал и её `FlowState`, вычитывает `IncomingPackets` и гонит каждый пакет через `GatewayPacketPipeline.TryProcessPacket`. Если бандл вернул `false` — пакет невалиден, канал отключается. После каждого пакета — flush ответа в сокет.

## Конвейер бандлов

**`GatewayPacketPipeline`** — сервис-обёртка над `PacketPipeline`, инжектится `[DI]` в `PacketDispatchSystem`. Внутри держит массив `PacketBundle` (пока пустой — бандлы в разработке). `TryProcessPacket(packet, writer, ref state)` делегирует текущему бандлу по `state.BundleIndex`. Бандл может сдвинуть `state.BundleIndex` для перехода к следующей фазе (Login → Configuration).

Концепция: каждая фаза Minecraft — отдельный `PacketBundle` со своими пакетами и логикой перехода. Status обслуживается отдельно (в `GuestScreeningSystem`, без сущности), а Login/Configuration — через конвейер, по одной сущности на игрока.

## Handler

`GatewayIntakeHandler` — stateless-помощник для `GuestScreeningSystem`. `TryParseHandshake(packet, out HandshakeData)` — парсит Handshake-пакет (0x00): protocolVersion, serverAddress, serverPort, nextState. Возвращает `1`/`2`/`-1`. `TryHandleStatusRequest(packet, writer)` — обрабатывает Status Request (0x00, отдаёт JSON из `ServerInfoCacheStore`) и Ping (0x01, эхо long). Берёт `ServerInfoCacheStore` из GLOBAL-мира — это и есть точка, где Gateway видит Global.

## Текущие ограничения

- Бандлы Login/Configuration не написаны: `GatewayPacketPipeline` инициализирован пустым массивом. Любой пакет от залогиненного игрока сейчас уходит в кик — `TryProcessPacket` возвращает `false` на `BundleIndex` за пределами пустого массива.
- Send-сторона синхронна: `channel.Writer.FlushAsync().GetAwaiter().GetResult()` в `GuestScreeningSystem` и `PacketDispatchSystem`. Один медленный писатель встаёт — встаёт весь тик Gateway, что контр-продуктивно идее backpressure. Запланирован переход на send-очередь с воркером.
