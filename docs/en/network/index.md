# The Network layer

Network is a passive byte pump. It accepts TCP connections, slices the incoming stream into packets (`RawPacket`), and enqueues them on the channel. It knows nothing about Minecraft phases, encryption, or compression — that is the layers' job. Outbound, it writes bytes from the `PipeWriter` into the socket. Built on `Pipelines.Sockets.Unofficial` (raw sockets + `System.IO.Pipelines`).

The core principle is **thread/ECS decoupling**. Leopotam.EcsProto is not thread-safe, so the accept thread in Network never touches the ECS world. It only pushes a `RawPacket` into a `ConcurrentQueue` on the channel; the sole writer of the world is an ECS system in the main tick. This decoupling is what enables backpressure: if the Gateway ECS tick is paused (DDoS), the sockets keep accepting packets and queueing them, and nothing is lost.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## Types

**`TcpNetworkService`** — a service, injected with `[DI]` into Gateway systems. Owns the listening socket and two public queues: `PendingConnections` (new channels awaiting Handshake) and `DisconnectedChannels` (dead channels — disconnect events). `Start(port)` binds and listens, then starts a background accept loop. Each accepted socket is wrapped into a `NetworkChannel`, pushed into `PendingConnections`, and gets its own `ProcessClientAsync` read loop. `Stop()` cancels the token and closes the listener.

**`NetworkChannel`** — a wrapper over a single connection: `Socket`, `PipeReader`, `PipeWriter`, and a `ConcurrentQueue<RawPacket> IncomingPackets`. `RemoteAddress` is a string like `"ip:port"` (for logs). `Disconnect()` is idempotent via `Interlocked.CompareExchange`: it completes the pipe and closes the socket exactly once. The channel is the only bridge between Network and the layers above: Gateway systems read from `IncomingPackets` and write the response into `Writer`.

**`RawPacket`** — `readonly struct (int Id, byte[] Data)`: packet id + payload, no length prefix. What a system gets out of the queue.

## Framing

Splitting the stream into packets happens in `TcpNetworkService.ProcessClientAsync`. The loop reads from the `PipeReader`, and `TryReadPacket` parses the `ReadOnlySequence<byte>` by Minecraft framing rules: VarInt length → VarInt id → payload. If the length or id is incomplete, it returns false, the buffer is not advanced, and we wait for more bytes. The payload itself is copied into a `byte[]` via `payloadSequence.CopyTo(data)`. After a successful split the buffer is advanced past the end of the packet (`buffer.Slice(payloadSequence.End)`).

```
read = await reader.ReadAsync(token)
buffer = read.Buffer
while TryReadPacket(ref buffer, out id, out data):
    channel.IncomingPackets.Enqueue(new RawPacket(id, data))
reader.AdvanceTo(buffer.Start, buffer.End)   # consumed = examined = end of what was inspected
```

Compression is not unwrapped here — it's the bundles' responsibility in Gateway. Network framing only knows about the length prefix.

## DataTypes and the Packet/ skeleton

`DataTypes/` — Minecraft encoding primitives operating on `SequenceReader<byte>` and `IBufferWriter<byte>`: `VarInt`/`VarLong` (LEB128, `TryRead` for partial reads), `Numeric` (Short/UShort/Int/Long/Float/Double, big-endian), `Utf8String` (VarInt length + UTF-8, with `ArrayPool` for multi-segment), `Boolean`, `Vector2`/`Vector3`. All methods are marked `[MethodImpl(AggressiveInlining)]`.

`Packet/` — the phase-conveyor skeleton that layers fill with their own bundles:

- **`RawPacket`** — the raw packet from the queue (see above).
- **`PacketBundle`** — an abstract class: one bundle = one protocol phase. `TryProcess(packet, writer, ref state)` decides what to do with a packet and writes the response into `writer`. It may advance `state.BundleIndex` to move to the next bundle (phase transition).
- **`PacketPipeline`** — an ordered array of bundles. `TryProcessPacket` picks the current bundle by `state.BundleIndex` and delegates to it.
- **`PacketFlowState`** — `struct (int BundleIndex, int StepIndex)`: where the entity is in the conveyor.
- **`PacketProcessor`** — an abstract class for processors inside a bundle; takes a `ProtoEntity` + `NetworkChannel` + `RawPacket`. The point where a bundle touches ECS.

This skeleton is neutral toward Minecraft: what counts as a phase and how to move between bundles is up to the layer. Gateway uses it for Handshake → Status → Login → Configuration.
