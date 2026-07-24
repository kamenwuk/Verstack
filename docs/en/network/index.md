# The Network layer

Network is the byte pump: it accepts TCP connections, turns the incoming byte stream into framed Minecraft payloads, hands each payload to the layer above, and writes framed responses back out. It owns three types — `TcpServer`, `SessionLifetime`, and the `IPacketHandler` contract — and is built on `Pipelines.Sockets.Unofficial` (raw sockets + `System.IO.Pipelines`).

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## TcpServer

`TcpServer` owns the listening socket. `Start()` binds and listens; `RunAsync()` runs the accept loop. For each accepted socket it creates a `SocketConnection` — which wraps the socket in a `Pipe` and starts background receive loops — then hands the connection off to `SessionLifetime`.

One limitation is worth knowing: `await session.RunAsync` blocks the accept loop, so the server currently handles a single connection at a time. Concurrency (a task per connection, or a pool) is a later milestone.

## SessionLifetime — the read loop

`SessionLifetime` drives one connection from connect to disconnect. It is constructed with an `IPacketHandler` and runs the read loop:

```
result = await reader.ReadAsync(token)
scanner = new PacketFrameScanner(result.Buffer)
while scanner.MoveNext():
    handler.OnPacket(scanner.Current, writer)   // handler writes into the buffer (sync)
// scanner is a ref struct — read its output into locals BEFORE the await below
consumed, status = scanner.ConsumedPosition, scanner.Status
reader.AdvanceTo(consumed, result.Buffer.End)
await writer.FlushAsync(token)
```

Two subtleties in this loop matter, both easy to get wrong.

The first is the scanner's lifetime. `PacketFrameScanner` is a `ref struct` — it holds a `SequenceReader`, which holds segment references, all of which must stay on the stack. A `ref struct` local cannot survive an `await`, because the compiler would have to hoist it into the state machine's fields. So `ConsumedPosition` and `Status` are read into plain value locals before `FlushAsync`; after that point the scanner is dead and the loop compiles. A fresh scanner is created on every `ReadAsync` anyway, because `result.Buffer` is invalidated by `AdvanceTo`.

The second is backpressure. `AdvanceTo(consumed, examined)` takes two args deliberately: `consumed` is the scanner's stop position, and on `Partial` the scanner points it at the start of the incomplete frame so its bytes stay buffered; `examined` is the end of the buffer, signalling "I looked at everything," which tells the pipe to keep reading.

## Flush point

The handler writes into the `PipeWriter` synchronously and returns; `SessionLifetime` calls `FlushAsync` once per read, after all frames in that read have been dispatched. This keeps the flush point in one place and leaves room for future batching.

## The IPacketHandler seam

```
public interface IPacketHandler
{
    void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}
```

`payload` is a complete frame's body, already stripped of the length prefix by the scanner. `output` is the write side of the connection; the handler writes framed responses here via `PacketFraming`. The method is synchronous — the handler only buffers; flushing is `SessionLifetime`'s job.

This interface is the entire contact surface between Network and Minecraft. Adding a new protocol state or packet type never touches Network — only a new `IPacketHandler` implementation and the wiring in `App`.
