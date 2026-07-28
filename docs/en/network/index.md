# The Network layer

Network is a passive byte pump. It accepts TCP connections, slices the incoming stream into packets (`RawPacket`), and enqueues them on the channel. It owns framing — including optional zlib compression — but knows nothing about Minecraft phases or encryption. Outbound, a send worker writes bytes from the queue into the socket. Built on `Pipelines.Sockets.Unofficial` (raw sockets + `System.IO.Pipelines`).

The core principle is **thread/ECS decoupling**. Leopotam.EcsProto is not thread-safe, so the accept thread in Network never touches the ECS world. It only pushes a `RawPacket` into a `ConcurrentQueue` on the channel; the sole writer of the world is an ECS system in the main tick. This decoupling is what enables backpressure: if the Gateway ECS tick is paused (DDoS), the sockets keep accepting packets and queueing them, and nothing is lost.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

## Types

**`TcpNetworkService`** — a service, injected with `[DI]` into Gateway systems. Owns the listening socket and two public queues: `PendingConnections` (new channels awaiting Handshake) and `DisconnectedChannels` (dead channels — disconnect events). Constructed with an `IPacketDecompressor` (read framing); if `null`, compressed inbound frames are rejected as Malformed. `Start(port)` binds and listens, then starts a background accept loop. Each accepted socket is wrapped into a `NetworkChannel`, pushed into `PendingConnections`, and gets its own `ProcessClientAsync` read loop and `SendLoopAsync` send worker. `Stop()` cancels the token and closes the listener.

**`NetworkChannel`** — a wrapper over a single connection: `Socket`, `PipeReader`, `PipeWriter`, a `ConcurrentQueue<RawPacket> IncomingPackets` (read → ECS) and an internal `OutboundQueue` (ECS → send worker, the sole `PipeWriter` owner). `RemoteAddress` is a string like `"ip:port"` (for logs). `Disconnect()` is idempotent via `Interlocked.CompareExchange`: it completes the pipe and closes the socket exactly once. `CompressionThreshold` is a `volatile int` (-1 by default): written by the ECS thread when a bundle enables compression, read by the read worker to switch framing. Like `RemoteAddress`, it is a cross-thread flag on the channel so the read worker does not have to enter the ECS world.

**`RawPacket`** — `readonly struct (int Id, byte[] Data)`: packet id + payload (after decompression, if any), no length prefix. What a system gets out of the queue.

## Framing — `PacketFrame`

Splitting the stream into packets lives in the static `PacketFrame`, not inline in the service. It handles both framing formats and per-channel compression:

- **Uncompressed framing** (when `channel.CompressionThreshold < 0`): `[VarInt(PacketLength)][VarInt(PacketId) + data]`.
- **Compressed framing** (after `Set Compression`): `[VarInt(PacketLength)][VarInt(DataLength) + payload]`, where `payload` is `[VarInt(PacketId) + data]` — uncompressed if the payload was below the threshold (`DataLength = 0`), or zlib-compressed if above (`DataLength` = original size).

`PacketFrame.TryRead(buffer, threshold, decompressor, out id, out data, out consumed)` returns a `PacketFrameResult`:

- `Complete` — a packet is ready; the caller advances the buffer to `consumed`.
- `Partial` — not enough bytes; the buffer is **not** advanced, we wait for more.
- `Malformed` — invalid length or corrupt zlib stream; the buffer is **not** advanced, and the read loop disconnects the channel (further parsing is pointless).

`TcpNetworkService.TryReadPacket` is a thin wrapper: it calls `PacketFrame.TryRead` and, on `Complete`, slices the buffer. The read loop in `ProcessClientAsync` distinguishes `Malformed` (disconnect) from `Partial` (wait), so a single corrupt frame no longer stalls the connection.

```
read = await reader.ReadAsync(token)
buffer = read.Buffer
loop:
    result = TryReadPacket(channel, ref buffer, out id, out data)
    if result == Malformed: disconnect; break
    if result != Complete:  break          # Partial — wait for more
    channel.IncomingPackets.Enqueue(new RawPacket(id, data))
reader.AdvanceTo(buffer.Start, buffer.End)
```

`PacketFrame.Write(ref SpanWriter, payload, compressor, threshold)` is the outbound counterpart. It wraps a ready payload (a `[VarInt(PacketId) + data]` blob built by a bundle) into the right frame for the channel's current threshold.

## DataTypes

`DataTypes/` — Minecraft encoding primitives on `SequenceReader<byte>` (read) and on both `IBufferWriter<byte>` and `ref SpanWriter` (write — two overloads per type, see below):

- `VarInt`/`VarLong` (LEB128, `TryRead` for partial reads).
- `Numeric` — Short/UShort/Int/Long/Float/Double, big-endian.
- `Utf8String` (VarInt length + UTF-8, `ArrayPool` for multi-segment).
- `Uuid` — 128-bit big-endian (RFC 4122) over `Guid`, using the .NET 9+ `bigEndian: true` overloads. Plus `GenerateOfflinePlayer(name)` — the vanilla offline-UUID: MD5 of the UTF-8 bytes of `"OfflinePlayer:<name>"`, with version-3 / RFC-4122 variant bits set (mirrors `java.util.UUID.nameUUIDFromBytes`).
- `PrefixedArray` — VarInt length + N elements, generic with read/write delegates. Cold path (Login/Configuration): array allocation is acceptable.
- `Boolean`, `Vector2`/`Vector3`.

All methods are marked `[MethodImpl(AggressiveInlining)]`.

## Outbound — `PacketOutbound` and `SpanWriter`

The contract a bundle uses to send packets. `PacketOutbound` is a `ref struct` living on the stack for the duration of one entity's processing; `PacketDispatchSystem` (Gateway) creates one per entity, backed by two heap buffers rented from `ArrayPool` for the whole tick:

- the **payload buffer** — where the bundle assembles the current packet via a local `SpanWriter`;
- the **frame buffer** — contiguous framing output, flushed to the channel as one chunk.

A bundle calls `outbound.Send(payload)`; `PacketFrame.Write` wraps `payload` per the channel's live threshold. This is why a single `TryProcess` call can mix uncompressed and compressed packets: Set Compression is sent before `EnableCompression` flips the threshold, so it goes out uncompressed, while the next packet is already compressed.

`SpanWriter` is a `ref struct` adapter of `Span<byte>` to the `GetSpan`/`Advance` shape. A `ref struct` cannot implement `IBufferWriter<byte>`, so each DataType has two write overloads — one for `IBufferWriter<byte>`, one for `ref SpanWriter`. The duplication is the deliberate price of a GC-free `ref struct` outbound.

## The Packet/ skeleton

`Packet/` also holds the phase-conveyor skeleton that layers fill with their own bundles:

- **`RawPacket`** — the raw packet from the queue (see above).
- **`PacketBundle`** — an abstract class: one bundle = one protocol phase. `TryProcess(stepIndex, entity, in packet, ref PacketOutbound outbound)` decides what to do with the packet and sends the response via `outbound`. The bundle does **not** touch `PacketFlowState` — the pipeline owns it. `StepCount` declares how many inbound packets the bundle expects.
- **`PacketPipeline`** — an ordered array of bundles. `TryProcessPacket` picks the current bundle by `state.BundleIndex`, delegates to it, and on success advances `state.StepIndex`; when `StepIndex >= StepCount`, it moves to `BundleIndex + 1`. `BundleCount` lets the dispatcher detect a finished conveyor (all phases passed). Returns `false` → the packet is invalid → the channel is kicked.
- **`PacketFlowState`** — `struct (int BundleIndex, int StepIndex)`: where the entity is in the conveyor.

This skeleton is neutral toward Minecraft: what counts as a phase and how to move between bundles is up to the layer. Gateway uses it for Handshake → Status → Login → Configuration.
