# Слой Network

Network — пассивный насос байт. Он принимает TCP-соединения, режет входящий поток на пакеты (`RawPacket`) и складывает их в очередь канала. Владеет фреймингом — включая опциональное zlib-сжатие, — но не знает ничего про Minecraft-фазы или шифрование. Обратно send-воркер пишет байты из очереди в сокет. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`).

Главный принцип — **развязка потоков и ECS**. Leopotam.EcsProto не потокобезопасен, поэтому accept-поток в Network никогда не трогает ECS-мир. Он только кладёт `RawPacket` в `ConcurrentQueue` на канале; единственный писатель мира — ECS-система в главном тике. Эта развязка и даёт backpressure: если ECS-тик Gateway встал на паузу (DDoS), сокеты продолжают принимать пакеты и копить их в очередях, ничего не теряется.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## Типы

**`TcpNetworkService`** — сервис, инжектится `[DI]` в системы Gateway. Владеет слушающим сокетом и двумя публичными очередями: `PendingConnections` (новые каналы, ждущие Handshake) и `DisconnectedChannels` (мертвые каналы — события отключения). Конструируется с `IPacketDecompressor` (read-фрейминг); если `null`, сжатые входящие кадры отбрасываются как Malformed. `Start(port)` биндит и слушает, запускает фоновый accept-цикл. Каждый принятый сокет оборачивается в `NetworkChannel`, кидается в `PendingConnections`, и для него запускаются `ProcessClientAsync` (цикл чтения) и `SendLoopAsync` (send-воркер). `Stop()` отменяет токен и закрывает слушатель.

**`NetworkChannel`** — обёртка над одним соединением: `Socket`, `PipeReader`, `PipeWriter`, `ConcurrentQueue<RawPacket> IncomingPackets` (read → ECS) и внутренняя `OutboundQueue` (ECS → send-воркер, единственный владелец `PipeWriter`). `RemoteAddress` — строка вида `"ip:port"` (для логов). `Disconnect()` идемпотентен через `Interlocked.CompareExchange`: завершает pipe и закрывает сокет один раз. `CompressionThreshold` — `volatile int` (-1 по умолчанию): пишется ECS-потоком, когда бандл включает compression, читается read-воркером для переключения фрейминга. Как и `RemoteAddress`, это cross-thread флаг на канале — чтобы read-воркеру не заходить в ECS-мир.

**`RawPacket`** — `readonly struct (int Id, byte[] Data)`: packet id + payload (после декомпрессии, если была) без префикса длины. То, что получает система из очереди.

## Фрейминг — `PacketFrame`

Резка потока на пакеты живёт в статическом `PacketFrame`, а не инлайн в сервисе. Он умеет оба формата и per-channel compression:

- **Несжатый framing** (когда `channel.CompressionThreshold < 0`): `[VarInt(PacketLength)][VarInt(PacketId) + data]`.
- **Compressed framing** (после Set Compression): `[VarInt(PacketLength)][VarInt(DataLength) + payload]`, где `payload` — это `[VarInt(PacketId) + data]`, несжатый (если payload был меньше threshold, `DataLength = 0`) или zlib-сжатый (если больше, `DataLength` = исходный размер).

`PacketFrame.TryRead(buffer, threshold, decompressor, out id, out data, out consumed)` возвращает `PacketFrameResult`:

- `Complete` — пакет готов; caller сдвигает буфер до `consumed`.
- `Partial` — данных мало; буфер **не** сдвигаем, ждём ещё.
- `Malformed` — некорректная длина или битый zlib-поток; буфер **не** сдвигаем, а read-цикл отключает канал (дальнейший парсинг бессмысленен).

`TcpNetworkService.TryReadPacket` — тонкая обёртка: вызывает `PacketFrame.TryRead` и при `Complete` режет буфер. Read-цикл в `ProcessClientAsync` различает `Malformed` (отключение) и `Partial` (ожидание), поэтому один битый кадр больше не вешает соединение.

```
read = await reader.ReadAsync(token)
buffer = read.Buffer
loop:
    result = TryReadPacket(channel, ref buffer, out id, out data)
    if result == Malformed: отключаем; break
    if result != Complete:  break          # Partial — ждём ещё
    channel.IncomingPackets.Enqueue(new RawPacket(id, data))
reader.AdvanceTo(buffer.Start, buffer.End)
```

`PacketFrame.Write(ref SpanWriter, payload, compressor, threshold)` — обратная сторона для отправки. Оборачивает готовый payload (blob `[VarInt(PacketId) + data]`, собранный бандлом) в правильный кадр по текущему threshold канала.

## DataTypes

`DataTypes/` — примитивы кодирования Minecraft на `SequenceReader<byte>` (чтение) и на обоих `IBufferWriter<byte>` и `ref SpanWriter` (запись — по две перегрузки на тип, см. ниже):

- `VarInt`/`VarLong` (LEB128, `TryRead` для partial-чтения).
- `Numeric` — Short/UShort/Int/Long/Float/Double, big-endian.
- `Utf8String` (VarInt-длина + UTF-8, с `ArrayPool` для multi-segment).
- `Uuid` — 128 бит big-endian (RFC 4122) поверх `Guid`, через перегрузки .NET 9+ `bigEndian: true`. Плюс `GenerateOfflinePlayer(name)` — offline-UUID ванильного сервера: MD5 от UTF-8 байт `"OfflinePlayer:<name>"` с установкой битов version=3 / variant RFC 4122 (повторяет `java.util.UUID.nameUUIDFromBytes`).
- `PrefixedArray` — VarInt-длина + N элементов, generic с read/write-делегатами. Cold path (Login/Configuration): аллокация массива допустима.
- `Boolean`, `Vector2`/`Vector3`.

Все методы помечены `[MethodImpl(AggressiveInlining)]`.

## Outbound — `PacketOutbound` и `SpanWriter`

Контракт, через который бандл отправляет пакеты. `PacketOutbound` — `ref struct`, живущий на стеке во время обработки одной сущности; `PacketDispatchSystem` (Gateway) создаёт его per-сущность, поверх двух heap-буферов, арендованных через `ArrayPool` на весь тик:

- **payload-буфер** — куда бандл собирает текущий пакет через локальный `SpanWriter`;
- **frame-буфер** — contiguous framing-выход, флашится в канал одним куском.

Бандл вызывает `outbound.Send(payload)`; `PacketFrame.Write` оборачивает `payload` по живому threshold канала. Поэтому один вызов `TryProcess` может смешивать несжатые и сжатые пакеты: Set Compression уходит до `EnableCompression` (которая переключает threshold), значит несжатым, а следующий пакет — уже сжатым.

`SpanWriter` — `ref struct`-адаптер `Span<byte>` к форме `GetSpan`/`Advance`. ref struct не может реализовать `IBufferWriter<byte>`, поэтому у каждого DataType по две write-перегрузки — под `IBufferWriter<byte>` и под `ref SpanWriter`. Дублирование — осознанная плата за GC-free `ref struct`-outbound.

## Каркас Packet/

`Packet/` также содержит каркас фазового конвейера, который слои наполняют своими бандлами:

- **`RawPacket`** — сырой пакет из очереди (см. выше).
- **`PacketBundle`** — абстрактный класс: один бандл = одна фаза протокола. `TryProcess(stepIndex, entity, in packet, ref PacketOutbound outbound)` решает, что делать с пакетом, и отправляет ответ через `outbound`. Бандл **не** трогает `PacketFlowState` — им владеет конвейер. `StepCount` объявляет, сколько входящих пакетов ждёт бандл.
- **`PacketPipeline`** — упорядоченный массив бандлов. `TryProcessPacket` берёт текущий бандл по `state.BundleIndex`, делегирует ему и при успехе двигает `state.StepIndex`; когда `StepIndex >= StepCount` — переходит к `BundleIndex + 1`. `BundleCount` позволяет диспетчеру определить пройденный конвейер (все фазы завершены). Возвращает `false` → пакет невалиден → канал кикается.
- **`PacketFlowState`** — `struct (int BundleIndex, int StepIndex)`: где находится сущность в конвейере.

Этот каркас нейтрален к Minecraft: что считать фазой и как переходить между бандлами — решает слой. Gateway использует его для Handshake → Status → Login → Configuration.
