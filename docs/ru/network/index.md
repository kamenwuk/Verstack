# Слой Network

Network — пассивный насос байт. Он принимает TCP-соединения, режет входящий поток на пакеты (`RawPacket`) и складывает их в очередь канала. Владеет фреймингом — включая опциональное zlib-сжатие, — но не знает ничего про Minecraft-фазы или шифрование. Обратно send-воркер пишет байты из очереди в сокет. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`).

Главный принцип — **развязка потоков и ECS**. Leopotam.EcsProto не потокобезопасен, поэтому accept-поток в Network никогда не трогает ECS-мир. О подключении и отключении он сообщает в `ClientLifecycleHandler` (его реализацию даёт [Bridge](../bridge/index.md)), а сам обработчик складывает события в потокобезопасные очереди. Единственный писатель ECS-миров — ECS-системы в главном тике. Пакеты тем временем копятся в `IncomingPackets` на каналах, ничего не теряется.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## Регистрация — `NetworkHubModule`

`NetworkHubModule : IProtoModule` — точка подключения транспорта к ECS. Конструируется с портом, `ClientLifecycleHandler`, `IPacketDecompressor` и `IPacketCompressor`; регистрирует в `IProtoSystems` декомпрессор, компрессор и `TcpNetworkService` (как init/destroy-сервис). Создаётся в `EntryPoint.Start` вместе с `BridgeHandoffRouter` (тот и есть `ClientLifecycleHandler`).

## Транспорт

**`TcpNetworkService`** — `internal sealed class`, init/destroy-сервис. Владеет слушающим сокетом и accept-циклом (`AcceptLoopAsync`). Каждый принятый сокет оборачивается в `NetworkChannel` и передаётся в `ClientLifecycleHandler.HandleConnect`; для канала запускаются `ProcessClientAsync` (цикл чтения) и `SendLoopAsync` (send-воркер, single-writer для `PipeWriter`). `Stop()` отменяет токен и закрывает слушатель.

**`NetworkChannel`** — `public class`, обёртка над одним соединением: `Socket`, `PipeReader`, `PipeWriter`, `ConcurrentQueue<RawPacket> IncomingPackets` (read → ECS) и внутренняя `OutboundQueue` (ECS → send-воркер). `RemoteAddress` — строка вида `"ip:port"` (для логов). `Disconnect()` идемпотентен. `CompressionThreshold` — `volatile int` (-1 по умолчанию): пишется ECS-потоком, когда бандл включает compression, читается read-воркером для переключения фрейминга. Cross-thread флаг на канале — чтобы read-воркеру не заходить в ECS-мир.

**`ClientLifecycleHandler`** — `public abstract class`, хуки жизненного цикла соединения: `HandleConnect(NetworkChannel)` и `HandleDisconnect(NetworkChannel)`. Вызываются из accept/read-потоков в `TcpNetworkService`; реализация в [Bridge](../bridge/index.md) (`BridgeHandoffRouter`) кладёт события в `ConcurrentQueue`, которые потом вычитываются ECS-системами.

**`RawPacket`** — `public readonly struct (int Id, byte[] Data)`: packet id + payload (после декомпрессии, если была) без префикса длины. То, что получает система из очереди. Метод `CreateReader()` возвращает `PacketStreamReader` поверх `Data`.

## Фрейминг — `PacketFrame`

Резка потока на пакеты живёт в статическом `PacketFrame`, а не инлайн в сервисе. Он умеет оба формата и per-channel compression:

- **Несжатый framing** (когда `channel.CompressionThreshold < 0`): `[VarInt(PacketLength)][VarInt(PacketId) + data]`.
- **Compressed framing** (после Set Compression): `[VarInt(PacketLength)][VarInt(DataLength) + payload]`, где `payload` — это `[VarInt(PacketId) + data]`, несжатый (если payload был меньше threshold, `DataLength = 0`) или zlib-сжатый (если больше, `DataLength` = исходный размер).

`PacketFrame.TryRead(...)` возвращает `PacketFrameResult`:

- `Complete` — пакет готов; caller сдвигает буфер до `consumed`.
- `Partial` — данных мало; буфер **не** сдвигаем, ждём ещё.
- `Malformed` — некорректная длина или битый zlib-поток; read-цикл отключает канал (дальнейший парсинг бессмысленен).

`PacketFrame.Write(ref PacketStreamWriter, payload, compressor, threshold)` — обратная сторона: оборачивает готовый payload в правильный кадр по текущему threshold канала.

## Чтение и запись — `PacketStreamReader` / `PacketStreamWriter`

Примитивы кодирования Minecraft разнесены по `Packet/Readers/` и `Packet/Writers/` и реализованы как extension-методы (синтаксис C# 14 `extension(ref PacketStreamReader streamReader) { ... }`) к двум `ref struct`:

- **`PacketStreamReader`** (`public ref struct`) — безаллокационный ридер поверх `RawPacket.Data`. Паттерн «deferred fault state»: при ошибке чтения не бросает сразу, а ставит `IsFaulted`/портит `IsValid`; бандл проверяет `reader.IsValid` после чтения и решает кикнуть. Свойства `Offset`, `Remaining`, `RemainingSpan`.
- **`PacketStreamWriter`** (`public ref struct`) — буфер записи поверх ArrayPool с авторасширением. Свойства `Written`, `WrittenSpan`. Методы возвращают `ref PacketStreamWriter` для chaining (`writer.WriteVarInt(0x00).WriteString(name).WriteUuid(uuid)`).

Extension-наборы (по файлу на категорию, есть и в Readers, и в Writers):

- **Numeric** — VarInt/VarLong (LEB128), Short/UShort/Int/Long/Float/Double, Byte/SByte, Bool (big-endian для fixed-width).
- **Text** — `TryReadString`/`ReadString` (VarInt-длина + UTF-8, max 32767×4 байт) и `WriteString`.
- **Geometry** — Uuid (128 бит big-endian, RFC 4122, через .NET 9+ `bigEndian: true`), Vector2/Vector3.
- **Raw** — `ReadByteRaw`/`ReadSpanRaw` и `WriteByte`/`WriteSpan` (сырые байты без кодирования).

Все методы помечены `[MethodImpl(AggressiveInlining)]`. `Uuid`-расширение также содержит `GenerateOfflinePlayer(name)` — offline-UUID ванильного сервера: MD5 от UTF-8 байт `"OfflinePlayer:<name>"` с установкой битов version=3 / variant RFC 4122 (повторяет `java.util.UUID.nameUUIDFromBytes`).

## Outbound — `PacketOutbound`

Контракт, через который бандл отправляет пакеты. `PacketOutbound` — `public ref struct : IDisposable`, создаваемый конвейером на сессию (поверх `NetworkChannel` + `IPacketCompressor`). Паттерн **Begin / Commit / Flush** с батчингом и zero-copy передачей массива в `OutboundQueue`:

```csharp
var writer = outbound.Begin();          // арендует буфер, возвращает PacketStreamWriter
writer.WriteVarInt(packetId).WriteInt(x).WriteString(name);
outbound.Commit(ref writer);            // рамит кадр, ставит в OutboundQueue одним куском
// ...ещё пакеты в той же сессии...
outbound.Flush();                       // сигнализирует send-воркеру
```

`EnableCompression(threshold)` переключает threshold канала — следующий кадр уже сжимается. `Commit` вызывает `PacketFrame.Write` с живым threshold'ом, поэтому один вызов `TryProcess` может смешивать несжатые и сжатые пакеты: Set Compression уходит до `EnableCompression`, значит несжатым, а следующий пакет — уже сжатым.

## Каркас Packet/Pipeline/

`Packet/Pipeline/` — каркас фазового конвейера, который слои наполняют своими бандлами. Нейтрален к Minecraft: что считать шагом/фазой и как переходить — решает слой.

- **`PacketBundle`** — `public abstract class`. Один бандл = связка шагов протокола. `TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)` решает, что делать с пакетом, и отправляет ответ через `outbound`. `StepCount` объявляет число шагов; `Init(IProtoSystems systems)` виртуальный — кэширует аспекты мира. Бандл **не** трогает `PacketFlowState` — им владеет конвейер.
- **`PacketFlowState`** — `public struct (int BundleIndex, int StepIndex)`: где находится сущность в конвейере. Хранится на сущности в пуле слоя.
- **`PacketHandleResult`** — `public enum { Accepted, Ignored, Kick, Continue }`. `Accepted` — шаг пройден, конвейер двигает `StepIndex`; `Ignored` — пакет проглочен без продвижения (легитимный, но посторонний в фазе пакет, напр. `minecraft:brand`); `Kick` — нарушение протокола; `Continue` — многошаговая отправка в одном пакете.
- **`PipelineSessionStatus`** — `public enum { Ok, Kick, Transfer }`. Результат всего вызова `ProcessSession`. `Transfer` — конвейер пройден до конца (только у Sequential).

Две реализации конвейера под два сценария:

**`SequentialPacketPipeline`** — `public sealed class`. Stateful-конвейер с жёсткой последовательностью шагов; прогресс хранится в `PacketFlowState` на сущности. Конструируется с массивом бандлов в фиксированном порядке; `ProcessSession(entity, channel, ref PacketFlowState state)` берёт текущий бандл по `state.BundleIndex`, делегирует ему и при `Accepted` двигает `StepIndex`; когда `StepIndex >= StepCount` — переходит к `BundleIndex + 1`. Достигнут конец массива → `Transfer`. Подходит для фаз с линейным протоколом (Handshake → Status → Login → Configuration, вход в Play). Используется Gateway (`PacketDispatchSystem`) и Realm (`UserEnterSystem`).

**`DispatchPacketPipeline`** — `public sealed class`. Stateless-диспетчер: маршрутизирует входящие пакеты по словарю `Dictionary<int, PacketBundle>` за O(1) по `packet.Id`, без строгого порядка. Создаёт **один** `PacketOutbound` на всю сессию (батчинг); при `Kick` делает `Flush()` и прерывается. Подходит для play-фазы, где пакеты приходят в произвольном порядке. Используется Realm (`SessionPacketRouterSystem`).

## Compression

`Compression/` — абстракции сжатия для framing-слоя:

- **`IPacketCompressor`** — `GetMaxCompressedSize(int)`, `Compress(ReadOnlySpan<byte>, Span<byte>)`. Инжектится `[DI(ServerWorldScopes.GLOBAL)]` в системы слоёв (живёт в GLOBAL-мире, см. [Bridge](../bridge/index.md) про кросс-скоуп DI).
- **`IPacketDecompressor`** — `Decompress(ReadOnlySequence<byte>, Span<byte>)`. Инжектится в `TcpNetworkService`.
- **`ZLibPacketCompressor` / `ZLibPacketDecompressor`** — реализации на `ZLibStream` (RFC 1950), `CompressionLevel.Optimal`. Создаются в `EntryPoint.Start`.
