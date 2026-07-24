# Architecture

Reference for the Verstack solution structure, layers, and dependencies.

## Solution layout

```
Verstack.slnx                          ← .NET 10 XML solution format
Directory.Build.props                  ← shared settings for all projects
src/
├── Verstack.Network/                  ← TCP/sockets + PipeReader loop. Depends on Protocol.
├── Verstack.Protocol/                 ← VarInt, framing. Pure logic, 0 NuGet deps.
└── Verstack.App/                      ← Program.cs, entry point. AssemblyName=Verstack
tests/
└── Verstack.Protocol.Tests/           ← xUnit, exercises Protocol via Span/Sequence
```

## Layers and dependency direction

```
App  →  Network  →  Protocol  →  (BCL only)
```

| Layer      | Knows about                          | Does NOT know about          |
|------------|--------------------------------------|------------------------------|
| `App`      | Network, Protocol                    | Minecraft semantics          |
| `Network`  | Protocol (`PacketFrameScanner`)      | Minecraft packet semantics   |
| `Protocol` | BCL only (`System.Buffers`)          | Sockets, Network, Minecraft  |

**Unbreakable rule:** Protocol never references Network. Protocol is tested in isolation
via `Span<byte>` / `ReadOnlySequence<byte>`, with no socket.

## Verstack.Network

Depends on `Pipelines.Sockets.Unofficial` (raw sockets + `System.IO.Pipelines`, by Marc Gravell).

| Type                 | Responsibility                                                       |
|----------------------|----------------------------------------------------------------------|
| `TcpServer`          | Listening socket + accept loop. Creates `SocketConnection`, hands off to `SessionLifetime`. |
| `SessionLifetime`    | Lifetime of one connection: `PipeReader` loop, framing via `PacketFrameScanner`, frame dispatch. |

### Read loop (SessionLifetime.RunAsync)

```
loop:
    ReadResult = await reader.ReadAsync(token)
    scanner = new PacketFrameScanner(result.Buffer)
    while scanner.MoveNext(): dispatch(scanner.Current)  // one payload = one Minecraft frame
    reader.AdvanceTo(scanner.ConsumedPosition, result.Buffer.End)
    if Malformed → drop connection
    if result.IsCompleted → break
reader.CompleteAsync()   // in finally
```

- `AdvanceTo(consumed, examined)` with two args — correct backpressure.
- On `Partial`, `ConsumedPosition` points to the start of the incomplete frame, so the tail stays buffered.
- One scanner per `ReadAsync` — `result.Buffer` is invalid after `AdvanceTo`.

## Verstack.Protocol

Pure logic, 0 NuGet dependencies. Tested via `Span<byte>` / `ReadOnlySequence<byte>`.

| Type                  | Responsibility                                                        |
|-----------------------|----------------------------------------------------------------------|
| `VarInt`              | LEB128 encode/decode of `int`. `Encode`/`TryDecode` on `Span`, `TryRead` on `SequenceReader`. Nested `ReadStatus` enum (`Complete`/`Partial`/`Malformed`). |
| `PacketFrameScanner`  | `ref struct` enumerator. Splits a `ReadOnlySequence<byte>` into complete Minecraft frames (VarInt-length-prefix). One-shot per `ReadAsync`. |

### Framing

Each frame on the wire:

```
[ VarInt: payload length ][ payload: N bytes ]
```

See [Protocol/VarInt](protocol/varint.md) for the LEB128 encoding.

## Verstack.App

Entry point (`Program.cs`). Builds `TcpServer`, wires Ctrl+C → `CancellationTokenSource`, runs `server.RunAsync(token)`.

## Current status

- ✅ TCP listener on 25565, accepts connections.
- ✅ Reads and frames incoming packets (`PacketFrameScanner`).
- ✅ End-to-end verified: real Minecraft client handshake (1.21.6) decoded correctly.
- ⬜ Packet writer / outbound packets (Status Response) — not yet implemented.
- ⬜ Handshake state machine — not yet implemented.
