# Network

The transport layer: TCP sockets, Minecraft packet framing, compression. Knows nothing about Minecraft phases — only
bytes and frames. Coupling with ECS worlds goes through [Bridge](bridge.md); Network doesn't invoke systems and sees no
entities.

The exit point from Network is the `ClientLifecycleHandler` abstraction (two methods: connect/disconnect), implemented
by `BridgeHandoffRouter`. All Network knows about the "world beyond transport" is this interface.

## TcpNetworkService

`internal sealed`, `IProtoInitService`/`IProtoDestroyService`. Registered in composition via `NetworkHubModule`
(see [engine/index.md](index.md#phase-1--building-the-global-world)), not exposed outward.

`Init` opens a `Socket` on the port (25565 by default) and starts the accept loop in a background thread. On each
accept: creates a `NetworkChannel`, calls `_clientLifecycleHandler.HandleConnect(channel)`, then launches two loops in
parallel — read and send. Both live until the channel disconnects.

`Destroy` tears down the `CancellationTokenSource` and closes the listening socket.

## NetworkChannel

One TCP connection. Two queues decouple the async threads and the single-threaded ECS tick (Leopotam is not thread-safe):

| Queue | Direction | Writer | Reader |
|-------|-----------|--------|--------|
| `IncomingPackets` (`ConcurrentQueue<RawPacket>`) | read-thread → ECS | `ProcessClientAsync` | the layer's game system (via Bridge) |
| `OutboundQueue` (`ConcurrentQueue<OutboundSegment>`) | ECS → send-thread | game system (`channel.EnqueueOutbound`) | `SendLoopAsync` |

Additionally, `_outboundSignal` (`SemaphoreSlim`) wakes the send worker when data appears in `OutboundQueue`. This is
needed because the send worker waits — otherwise ECS would have to write to `PipeWriter` directly, and the
`System.IO.Pipelines` contract requires **single writer**: the `Writer` owner is only the send worker.

`CompressionThreshold` (`volatile int`, `-1` by default — compression off). Changed via
`PacketOutbound.EnableCompression(threshold)` after the client receives `Set Compression` (in the Login phase).
Threshold 256 (`ServerConstants.COMPRESSION_THRESHOLD`).

`Disconnect()` is idempotent (`Interlocked.CompareExchange`): completes `Reader`/`Writer`, shuts down the socket, and
wakes the send worker (otherwise it would sleep in `WaitOutboundAsync` forever).

## Read loop

`ProcessClientAsync`: reads from `PipeReader`, slices the stream into frames via `PacketFrame.TryRead`. Results:

- `Complete` — packet parsed, `RawPacket` into `IncomingPackets`, buffer advances to `consumed`. The loop slices the next frame.
- `Partial` — too little data, leave the buffer alone, wait for the next `ReadAsync`.
- `Malformed` — broken frame (garbage length, corrupt zlib stream). Leave the buffer alone, but **disconnect** — further
  parsing is pointless.

`AdvanceTo(buffer.Start, buffer.End)` — examines to the end: everything not consumed stays, the rest is returned to the pipe.

On loop exit (any) — `channel.Disconnect()` and `_clientLifecycleHandler.HandleDisconnect(channel)` in `finally`.

## Send loop

`SendLoopAsync`: the sole owner of `PipeWriter`. Waits on `_outboundSignal`, drains `OutboundQueue`, writes each
`OutboundSegment` to `Writer`, flushes. The segment buffer is returned to `ArrayPool` in `finally`. On
`IOException`/`SocketException` — `Disconnect()` and exit.

## Framing

`PacketFrame` (static) — frame parsing and packing with `compressionThreshold` in mind. The outbound data contract is
the same for both modes: input is always `[VarInt(PacketId) + data]`; framing wraps it into the correct frame itself.

**Frame formats:**

- Uncompressed (`threshold < 0`): `[VarInt(PacketLength)][VarInt(PacketId) + data]`.
- Compressed framing (`threshold ≥ 0`): `[VarInt(PacketLength)][VarInt(DataLength) + payload]`, where payload is
  - `DataLength=0`, if packet size `< threshold` (packet uncompressed, the DataLength marker signals "no compression");
  - `DataLength=N`, if the packet is compressed: `payload` = zlib stream, decompressed length = N, inside
    `[VarInt(PacketId) + data]`.

`PacketFrameResult` — enum `Complete`/`Partial`/`Malformed`, drives read-loop behavior (above).

`RawPacket` (`readonly struct`) — parsed packet: `Id` + data array. `CreateReader()` returns a `PacketStreamReader`
over the data. This is what lands in `IncomingPackets` and reaches the game system.

`OutboundSegment` (`internal readonly struct`) — a chunk of bytes for sending. Buffer from `ArrayPool`, returned by the
send worker after writing. `Length` may be less than the array length (a rented buffer is often larger than the data).
Layers work through `channel.EnqueueOutbound(...)`, not through `OutboundSegment` directly.

## Compression

`IPacketCompressor` / `IPacketDecompressor` — abstractions for the framing layer. The default implementation is
`ZLibPacketCompressor` / `ZLibPacketDecompressor` (RFC 1950 / zlib format, via `ZLibStream`).

`IPacketCompressor.GetMaxCompressedSize(sourceLength)` — an upper bound (`compressBound` from zlib docs) for reserving
a buffer before compression. `Compress(source, destination)` writes into `Span<byte>`, returns bytes written.

Compressors are registered as services in `NetworkHubModule` and reach game systems via `[DI(ServerWorldScopes.GLOBAL)]`.

## Readers and writers

`PacketStreamReader` and `PacketStreamWriter` — `ref struct` over `ArrayPool` buffers. No boxing, stack only.

`PacketStreamWriter` (`internal`): auto-growth via `EnsureCapacity` (doubling size, returning the old buffer to the
pool), `Advance`/`Reset`. The actual write methods (`WriteVarInt`, `WriteString`, `WriteVector3i`, etc.) are in the
extension classes `PacketWriter{Numeric,Geometry,Text,Raw}Extensions`.

`PacketStreamReader` (`internal`): the **Deferred Fault State** pattern. On a read error (reading more than available,
invalid VarInt) the reader **doesn't throw** — it sets `_isFaulted = true`. All subsequent read calls instantly return
default without touching memory. The caller checks `IsValid` after all reads. This removes exception overhead in the
hot path when handling broken packets. Read methods are in `PacketReader{...}Extensions`.

## What's available to layers

| Type | Access | What layers do with it |
|------|--------|------------------------|
| `NetworkChannel` | `public` | Obtained via `BridgeStateCacheStore` for an entity; written via `EnqueueOutbound` |
| `RawPacket` | `public` | Read from `IncomingPackets` (via Bridge); `CreateReader()` for parsing |
| `PacketOutbound` | `public` (`internal` ctor) | Build an outbound packet: `Begin/Commit/Flush` |
| `PacketFrame` | `public` | Transparently used inside `PacketOutbound` |
| `IPacketCompressor`/`Decompressor` | `public` | Via `[DI(GLOBAL)]`, for `PacketOutbound` |
| `TcpNetworkService` | `internal` | Unavailable; only inside `NetworkHubModule` |
| `OutboundSegment` | `internal` | Unavailable; layers write via `EnqueueOutbound` |
| `PacketStreamReader/Writer` | `internal` ctor | Created via `RawPacket.CreateReader()` / `PacketOutbound.Begin()` |
