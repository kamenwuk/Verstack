# Жизнь соединения

Два типа несут соединение от accept'а до отключения. `TcpServer` владеет слушающим сокетом и accept-циклом; `SessionLifetime` — циклом чтения/записи одного соединения.

## TcpServer

`TcpServer` владеет слушающим сокетом. `Start()` биндит и слушает; `AcceptConnectionsAsync()` крутит accept-цикл. Для каждого принятого сокета он создаёт `SocketConnection` (тот оборачивает сокет в `Pipe` и запускает фоновые циклы приёма) и запускает фоновую задачу `HandleConnectionAsync`, которая владеет соединением всю его жизнь: вызывает `_factory.Create()` для свежего handler'а, передаёт пару в `SessionLifetime` и диспозит соединение в `finally`. Accept-цикл не блокируется одиночным соединением — сессии идут параллельно.

Активные задачи копятся в `_sessionTasks` (под `Lock`). Каждая снимает себя из списка по завершении (fire-and-forget `ContinueWith` с `ExecuteSynchronously`). При graceful shutdown — отмене токена или закрытии слушающего сокета — `AcceptConnectionsAsync` выходит из цикла и через `Task.WhenAll` дожидается ещё живых соединений; процесс не выйдет, пока все не закроются.

## SessionLifetime — цикл чтения

`SessionLifetime` ведёт одно соединение от подключения до отключения. Он конструируется с `IPacketHandler` и крутит цикл чтения:

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

→ [Packet handler](packet-handler.md)
