# Слой Network

Network — это насос байт: он принимает TCP-соединения, превращает входящий поток байт в обрамлённые payload'ы Minecraft, передаёт каждый payload в вышележащий слой и пишет обрамлённые ответы обратно. Владеет тремя типами — `TcpServer`, `SessionLifetime` и контрактом `IPacketHandler` — и построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`).

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## TcpServer

`TcpServer` владеет слушающим сокетом. `Start()` биндит и слушает; `RunAsync()` крутит accept-цикл. Для каждого принятого сокета он создаёт `SocketConnection` — тот оборачивает сокет в `Pipe` и запускает фоновые циклы приёма — и передаёт соединение в `SessionLifetime`.

Стоит знать одно ограничение: `await session.RunAsync` блокирует accept-цикл, поэтому сейчас сервер держит по одному соединению за раз. Конкурентность (задача на соединение или пул) — отдельный milestone.

## SessionLifetime — цикл чтения

`SessionLifetime` ведёт одно соединение от подключения до отключения. Конструируется с `IPacketHandler` и крутит цикл чтения:

```
result = await reader.ReadAsync(token)
scanner = new PacketFrameScanner(result.Buffer)
while scanner.MoveNext():
    handler.OnPacket(scanner.Current, writer)   // handler пишет в буфер (sync)
// scanner — ref struct, вычитываем его результат в локалы ДО await ниже
consumed, status = scanner.ConsumedPosition, scanner.Status
reader.AdvanceTo(consumed, result.Buffer.End)
await writer.FlushAsync(token)
```

В этом цикле важны два тонких места, оба легко испортить.

Первое — время жизни scanner'а. `PacketFrameScanner` — `ref struct`: он держит `SequenceReader`, тот держит ссылки на сегменты, и всё это обязано оставаться на стеке. Локал типа `ref struct` не может пережить `await`, потому что компилятору пришлось бы поднять его в поля state machine. Поэтому `ConsumedPosition` и `Status` вычитываются в обычные value-локалы до `FlushAsync`; после этой точки scanner «мёртв», и цикл компилируется. Свежий scanner и так создаётся на каждый `ReadAsync`, потому что `result.Buffer` инвалидируется вызовом `AdvanceTo`.

Второе — backpressure. `AdvanceTo(consumed, examined)` принимает два аргумента не просто так: `consumed` — точка остановки scanner'а, и при `Partial` scanner указывает им на начало недочитанного кадра, чтобы его байты остались в буфере; `examined` — конец буфера, сигнал «я просмотрел всё», по которому pipe продолжает читать.

## Точка flush'а

Handler пишет в `PipeWriter` синхронно и возвращает управление; `SessionLifetime` вызывает `FlushAsync` один раз за чтение, после того как все кадры из этого чтения диспетчеризованы. Так точка flush'а собрана в одном месте и оставляет пространство для будущего батчинга.

## Шов IPacketHandler

```
public interface IPacketHandler
{
    void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}
```

`payload` — тело целого кадра, уже очищенное от префикса длины scanner'ом. `output` — сторона записи соединения; handler пишет сюда обрамлённые ответы через `PacketFraming`. Метод синхронный — handler только буферизует; flush — задача `SessionLifetime`.

Этот интерфейс — вся поверхность контакта между Network и Minecraft. Добавление нового состояния протокола или типа пакета никогда не затрагивает Network — только новую реализацию `IPacketHandler` и связку в `App`.
