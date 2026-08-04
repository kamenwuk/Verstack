# Network

Транспортный слой: TCP-сокеты, фрейминг пакетов Minecraft, компрессия. Не знает про Minecraft-фазы — только байты и кадры.
Связь с ECS-мирами идёт через [Bridge](bridge.md); Network не дёргает системы и не видит сущностей.

Точка выхода из Network — абстракция `ClientLifecycleHandler` (два метода: connect/disconnect), реализованная
`BridgeHandoffRouter`. Всё, что Network знает про «мир за пределами транспорта» — это этот интерфейс.

## TcpNetworkService

`internal sealed`, `IProtoInitService`/`IProtoDestroyService`. Регистрируется в композиции через `NetworkHubModule`
(см. [engine/index.md](index.md#фаза-1--создание-мира-global)), наружу не торчит.

`Init` открывает `Socket` на порту (по умолчанию 25565), запускает accept-цикл в фоновом потоке. На каждый accept:
создаёт `NetworkChannel`, вызывает `_clientLifecycleHandler.HandleConnect(channel)`, затем параллельно запускает два
цикла — read и send. Оба живут до отключения канала.

`Destroy` гасит `CancellationTokenSource` и закрывает слушающий сокет.

## NetworkChannel

Одно TCP-соединение. Две очереди развязывают асинхронные потоки и однопоточный ECS-тик (Leopotam не потокобезопасен):

| Очередь | Направление | Кто пишет | Кто читает |
|---------|-------------|-----------|------------|
| `IncomingPackets` (`ConcurrentQueue<RawPacket>`) | read-поток → ECS | `ProcessClientAsync` | игровая система слоя (через Bridge) |
| `OutboundQueue` (`ConcurrentQueue<OutboundSegment>`) | ECS → send-поток | игровая система (`channel.EnqueueOutbound`) | `SendLoopAsync` |

Дополнительно `_outboundSignal` (`SemaphoreSlim`) будит send-воркер, когда в `OutboundQueue` появились данные. Это нужно,
потому что send-воркер ждёт — иначе ECS пришлось бы писать в `PipeWriter` напрямую, а контракт `System.IO.Pipelines`
требует **single writer**: владельцем `Writer` является только send-воркер.

`CompressionThreshold` (`volatile int`, по умолчанию `-1` — сжатие выключено). Меняется через `PacketOutbound.EnableCompression(threshold)`
после получения клиентом `Set Compression` (в фазе Login). Порог 256 (`ServerConstants.COMPRESSION_THRESHOLD`).

`Disconnect()` идемпотентен (`Interlocked.CompareExchange`): завершает `Reader`/`Writer`,.shutdown'ит сокет и будит
send-воркер (иначе он уснёт в `WaitOutboundAsync` навсегда).

## Read-цикл

`ProcessClientAsync`: читает из `PipeReader`, режет поток на кадры через `PacketFrame.TryRead`. Результаты:

- `Complete` — пакет разобран, `RawPacket` в `IncomingPackets`, буфер сдвигается до `consumed`. Цикл режет следующий кадр.
- `Partial` — данных мало, буфер не трогать, ждём следующий `ReadAsync`.
- `Malformed` — кадр битый (мусорная длина, битый zlib-поток). Буфер не трогать, но **соединение отключить** —
  дальнейший парсинг бессмысленен.

`AdvanceTo(buffer.Start, buffer.End)` — examin'ит до конца: всё неотconsumeённое остаётся, остальное возвращается в pipe.

При выходе из цикла (любом) — `channel.Disconnect()` и `_clientLifecycleHandler.HandleDisconnect(channel)` в `finally`.

## Send-цикл

`SendLoopAsync`: единственный владелец `PipeWriter`. Ждёт `_outboundSignal`, вычитывает `OutboundQueue`, пишет каждый
`OutboundSegment` в `Writer`, флашит. Буфер сегмента возвращается в `ArrayPool` в `finally`. При `IOException`/`SocketException` —
`Disconnect()` и выход.

## Фрейминг

`PacketFrame` (статический) — разбор и упаковка кадров с учётом `compressionThreshold`. Контракт исходящих данных
одинаковый для обоих режимов: на вход всегда `[VarInt(PacketId) + data]`, framing сам оборачивает в правильный кадр.

**Форматы кадров:**

- Несжатый (`threshold < 0`): `[VarInt(PacketLength)][VarInt(PacketId) + data]`.
- Compressed framing (`threshold ≥ 0`): `[VarInt(PacketLength)][VarInt(DataLength) + payload]`, где payload —
  - `DataLength=0`, если размер пакета `< threshold` (пакет несжатый, маркер DataLength сигнализирует «сжатия нет»);
  - `DataLength=N`, если пакет сжатый: `payload` = zlib-поток, распакованная длина = N, внутри `[VarInt(PacketId) + data]`.

`PacketFrameResult` — enum `Complete`/`Partial`/`Malformed`, управляет поведением read-цикла (выше).

`RawPacket` (`readonly struct`) — разобранный пакет: `Id` + массив данных. `CreateReader()` возвращает `PacketStreamReader`
поверх данных. Это то, что попадает в `IncomingPackets` и доходит до игровой системы.

`OutboundSegment` (`internal readonly struct`) — порция байтов на отправку. Буфер из `ArrayPool`, возвращается
send-воркером после записи. `Length` может быть меньше длины массива (арендованный буфер часто больше данных). Слои
работают через `channel.EnqueueOutbound(...)`, а не через `OutboundSegment` напрямую.

## Компрессия

`IPacketCompressor` / `IPacketDecompressor` — абстракции для framing-слоя. Реализация по умолчанию —
`ZLibPacketCompressor` / `ZLibPacketDecompressor` (формат RFC 1950 / zlib, через `ZLibStream`).

`IPacketCompressor.GetMaxCompressedSize(sourceLength)` — верхняя оценка (`compressBound` из zlib-документации) для
резервирования буфера до сжатия. `Compress(source, destination)` пишет в `Span<byte>`, возвращает число записанных байт.

Компрессоры регистрируются как сервисы в `NetworkHubModule` и доходят до игровых систем через `[DI(ServerWorldScopes.GLOBAL)]`.

## Читатели и писатели

`PacketStreamReader` и `PacketStreamWriter` — `ref struct` поверх `ArrayPool`-буферов. Без упаковки (boxing), только стек.

`PacketStreamWriter` (`internal`): автo-расширение через `EnsureCapacity` (удвоение размера, возврат старого буфера в пул),
`Advance`/`Reset`. Реальные методы записи (`WriteVarInt`, `WriteString`, `WriteVector3i` и т.д.) — в extension-классах
`PacketWriter{Numeric,Geometry,Text,Raw}Extensions`.

`PacketStreamReader` (`internal`): паттерн **Deferred Fault State**. При ошибке чтения (читают больше доступного,
невалидный VarInt) ридер **не бросает исключение** — переходит в `_isFaulted = true`. Все последующие вызовы чтения
мгновенно возвращают default без работы с памятью. Вызывающий код проверяет `IsValid` после всех чтений. Это убирает
накладные расходы на исключения при обработке битых пакетов в горячем пути. Методы чтения — в `PacketReader{...}Extensions`.

## Что доступно слоям

| Тип | Доступ | Что с ним делают слои |
|------|--------|----------------------|
| `NetworkChannel` | `public` | Через `BridgeStateCacheStore` получают канал сущности; пишут через `EnqueueOutbound` |
| `RawPacket` | `public` | Читают из `IncomingPackets` (через Bridge); `CreateReader()` для разбора |
| `PacketOutbound` | `public` (`internal` ctor) | Формируют исходящий пакет: `Begin/Commit/Flush` |
| `PacketFrame` | `public` | Прозрачно используется внутри `PacketOutbound` |
| `IPacketCompressor`/`Decompressor` | `public` | Через `[DI(GLOBAL)]`, для `PacketOutbound` |
| `TcpNetworkService` | `internal` | Недоступен; только внутри `NetworkHubModule` |
| `OutboundSegment` | `internal` | Недоступен; слои пишут через `EnqueueOutbound` |
| `PacketStreamReader/Writer` | `internal` ctor | Создаются через `RawPacket.CreateReader()` / `PacketOutbound.Begin()` |
