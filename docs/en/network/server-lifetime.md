# Server lifetime

Two types carry a connection from accept to disconnect. `TcpServer` owns the listening socket and the accept loop; `SessionLifetime` owns the read/write loop of one connection.

## TcpServer

`TcpServer` owns the listening socket. `Start()` binds and listens; `AcceptConnectionsAsync()` runs the accept loop. For each accepted socket it creates a `SocketConnection` (which wraps the socket in a `Pipe` and starts background receive loops) and spawns a background task `HandleConnectionAsync` that owns the connection for its whole lifetime: it calls `_factory.Create()` for a fresh handler, hands the pair off to `SessionLifetime`, and disposes the connection in `finally`. The accept loop is not blocked by a single connection — sessions run in parallel.

Active tasks accumulate in `_sessionTasks` (under a `Lock`). Each one removes itself from the list on completion (a fire-and-forget `ContinueWith` with `ExecuteSynchronously`). On graceful shutdown — token cancellation or closing the listening socket — `AcceptConnectionsAsync` exits the loop and awaits still-running sessions via `Task.WhenAll`; the process will not exit until all of them close.

## SessionLifetime — the read loop

`SessionLifetime` drives one connection from connect to disconnect. It is constructed with an `IPacketHandler` and runs the read loop:

```
result = await reader.ReadAsync(token)
scanner = new PacketFrameScanner(result.Buffer)
drop = false
while scanner.MoveNext():
    if handler.OnPacket(scanner.Current, writer) == Disconnect:   // handler writes into the buffer (sync)
        drop = true
        break
// scanner is a ref struct — read its output into locals BEFORE the await below
consumed, status = scanner.ConsumedPosition, scanner.Status
reader.AdvanceTo(consumed, result.Buffer.End)
await writer.FlushAsync(token)
if drop or status == Malformed:
    break    // tear the connection down
```

Two subtleties in this loop matter, both easy to get wrong.

The first is the scanner's lifetime. `PacketFrameScanner` is a `ref struct` — it holds a `SequenceReader`, which holds segment references, all of which must stay on the stack. A `ref struct` local cannot survive an `await`, because the compiler would have to hoist it into the state machine's fields. So `ConsumedPosition` and `Status` are read into plain value locals before `FlushAsync`; after that point the scanner is dead and the loop compiles. A fresh scanner is created on every `ReadAsync` anyway, because `result.Buffer` is invalidated by `AdvanceTo`.

The second is backpressure. `AdvanceTo(consumed, examined)` takes two args deliberately: `consumed` is the scanner's stop position, and on `Partial` the scanner points it at the start of the incomplete frame so its bytes stay buffered; `examined` is the end of the buffer, signalling "I looked at everything," which tells the pipe to keep reading.

## Flush point

The handler writes into the `PipeWriter` synchronously and returns; `SessionLifetime` calls `FlushAsync` once per read, after all frames in that read have been dispatched. This keeps the flush point in one place and leaves room for future batching.

## The drop point

The loop exits and tears the connection down in two cases: the handler returned `PacketVerdict.Disconnect` for a specific frame, or the scanner returned `VarInt.ReadStatus.Malformed` (a frame with a broken length prefix). These are two different sources of "garbage," but both branches converge into a single `break`. A handler-requested Disconnect breaks out of the scanner loop and reaches the same check **after** the flush — so a response written by the handler for that frame still goes out before the disconnect. What counts as a "garbage" frame is the handler's call (see [the dispatcher](../minecraft/dispatcher.md)); `SessionLifetime` merely honors the verdict. Pipe cleanup (`CompleteAsync`) is always in `finally`.

→ [Packet handler](packet-handler.md)
