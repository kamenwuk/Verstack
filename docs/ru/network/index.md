# Слой Network

Network — пассивный насос байт. Он принимает TCP-соединения, режет входящий поток на пакеты (`RawPacket`) и складывает их в очередь канала. Не знает ничего про Minecraft-фазы, шифрование или сжатие — это забота слоёв. Обратно он пишет байты из `PipeWriter`'а в сокет. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`).

Главный принцип — **развязка потоков и ECS**. Leopotam.EcsProto не потокобезопасен, поэтому accept-поток в Network никогда не трогает ECS-мир. Он только кладёт `RawPacket` в `ConcurrentQueue` на канале; единственный писатель мира — ECS-система в главном тике. Эта развязка и даёт backpressure: если ECS-тик Gateway встал на паузу (DDoS), сокеты продолжают принимать пакеты и копить их в очередях, ничего не теряется.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## Типы

**`TcpNetworkService`** — сервис, инжектится `[DI]` в системы Gateway. Владеет слушающим сокетом и двумя публичными очередями: `PendingConnections` (новые каналы, ждущие Handshake) и `DisconnectedChannels` (мертвые каналы — события отключения). `Start(port)` биндит и слушает, запускает фоновый accept-цикл. Каждый принятый сокет оборачивается в `NetworkChannel`, кидается в `PendingConnections`, и для него запускается `ProcessClientAsync` — цикл чтения. `Stop()` отменяет токен и закрывает слушатель.

**`NetworkChannel`** — обёртка над одним соединением: `Socket`, `PipeReader`, `PipeWriter` и `ConcurrentQueue<RawPacket> IncomingPackets`. `RemoteAddress` — строка вида `"ip:port"` (для логов). `Disconnect()` идемпотентен через `Interlocked.CompareExchange`: завершает pipe и закрывает сокет один раз. Канал — единственный мост между Network и вышележащими слоями: системы Gateway читают из `IncomingPackets` и пишут ответ в `Writer`.

**`RawPacket`** — `readonly struct (int Id, byte[] Data)`: packet id + payload без префикса длины. То, что получает система из очереди.

## Фрейминг

Резка потока на пакеты — в `TcpNetworkService.ProcessClientAsync`. Цикл читает из `PipeReader`, а `TryReadPacket` разбирает `ReadOnlySequence<byte>` по правилам Minecraft-фрейминга: VarInt-длина → VarInt-id → payload. Если длины или id не хватает — возвращает false, буфер не сдвигается, ждём следующих байт. Сам payload копируется в `byte[]` через `payloadSequence.CopyTo(data)`. После успешного реза буфер сдвигается за конец пакета (`buffer.Slice(payloadSequence.End)`).

```
read = await reader.ReadAsync(token)
buffer = read.Buffer
while TryReadPacket(ref buffer, out id, out data):
    channel.IncomingPackets.Enqueue(new RawPacket(id, data))
reader.AdvanceTo(buffer.Start, buffer.End)   # consumed = examined = конец того, что просмотрено
```

Сжатие здесь не разворачивается — это ответственность бандлов в Gateway. Фрейминг Network знает только про префикс длины.

## DataTypes и каркас Packet/

`DataTypes/` — примитивы кодирования Minecraft, работающие с `SequenceReader<byte>` и `IBufferWriter<byte>`: `VarInt`/`VarLong` (LEB128, `TryRead` для partial-чтения), `Numeric` (Short/UShort/Int/Long/Float/Double, big-endian), `Utf8String` (VarInt-длина + UTF-8, с `ArrayPool` для multi-segment), `Boolean`, `Vector2`/`Vector3`. Все методы помечены `[MethodImpl(AggressiveInlining)]`.

`Packet/` — каркас фазового конвейера, который слои наполняют своими бандлами:

- **`RawPacket`** — сырой пакет из очереди (см. выше).
- **`PacketBundle`** — абстрактный класс: один бандл = одна фаза протокола. `TryProcess(packet, writer, ref state)` решает, что делать с пакетом, и пишет ответ в `writer`. Может двигать `state.BundleIndex` — переход к следующему бандлу (смена фазы).
- **`PacketPipeline`** — упорядоченный массив бандлов. `TryProcessPacket` берёт текущий бандл по `state.BundleIndex` и делегирует ему.
- **`PacketFlowState`** — `struct (int BundleIndex, int StepIndex)`: где находится сущность в конвейере.
- **`PacketProcessor`** — абстрактный класс для процессоров внутри бандла, принимает `ProtoEntity` + `NetworkChannel` + `RawPacket`. Точка, где бандл касается ECS.

Этот каркас нейтрален к Minecraft: что считать фазой и как переходить между бандлами — решает слой. Gateway использует его для Handshake → Status → Login → Configuration.
