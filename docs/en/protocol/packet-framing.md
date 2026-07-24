# Packet Framing

Framing is what splits a raw TCP byte stream into discrete Minecraft packets. This document covers the framing scheme used by Minecraft and the `PacketFrameScanner` that implements it.

## The problem: TCP is a stream, not a sequence of messages

TCP delivers an ordered, reliable **stream of bytes** — it has no concept of message boundaries. When you call `ReadAsync` on a `PipeReader`, the chunk you get back can be anything:

```
ReadAsync returned 100 bytes. What is inside?
├─ maybe 3 complete packets         [len][payload][len][payload][len][payload]
├─ maybe 1 complete + half a 2nd    [len][payload][len][paylo...
├─ maybe only half of one packet    [len][paylo...
└─ maybe tail of one + start of next ...oad][len][paylo...
```

Without a rule for where one packet ends and the next begins, the bytes are meaningless. That rule is **framing**.

## Minecraft framing: VarInt-length-prefix

Every packet on the wire is structured as:

```
[ VarInt: payload length ][ payload: N bytes ]
         ↑ up to 5 bytes        ↑ exactly N bytes
```

To extract one frame:

1. Read a VarInt → learn `length` (the size of the payload in bytes).
2. Check whether at least `length` bytes follow it.
   - **yes** → complete frame. Take those `length` bytes as the payload.
   - **no** → not enough data yet. Wait for the buffer to fill.
3. The payload itself starts with a VarInt packet ID, followed by the packet's fields — but that is packet parsing, not framing. Framing stops at extracting the `length`-byte payload.

## A frame on the wire

A minimal example — a 3-byte payload `[0x10, 0x00, 0xFF]`:

```
VarInt(3) = 0x03        ← payload length, single byte
payload   = 10 00 FF    ← 3 bytes

wire bytes: [ 03 10 00 FF ]
```

A two-byte length (payload of 300 bytes) spans a multi-byte VarInt:

```
VarInt(300) = AC 02     ← payload length, two bytes
payload     = ...300 bytes...

wire bytes: [ AC 02 <300 bytes> ]
```

See [VarInt](varint.md) for how `300` becomes `[AC 02]`.

## Partial data is normal

In streaming I/O, running out of bytes mid-frame is **expected**, not exceptional. A frame can arrive split across many `ReadAsync` calls. There are two ways a frame can be incomplete:

| Situation | What happened |
|-----------|---------------|
| VarInt not terminated | The length prefix itself is cut: continuation bits set, but the terminating byte hasn't arrived. |
| Payload not full | The VarInt was read completely (we know `length`), but fewer than `length` payload bytes are buffered. |

In both cases the right answer is the same: **stop, remember where the incomplete frame started, wait for more data, retry.**

## Corrupted data

Two failure modes mean the data is genuinely broken (or someone is sending garbage):

| Situation | What happened |
|-----------|---------------|
| VarInt overflow | Continuation never terminates within `MAX_SIZE` (5) bytes — impossible for a valid `int32`. |
| Length too large | `length` exceeds the configured `MaxPacketSize` (default ~2 MB) — a memory-exhaustion attack. |

In both cases the connection should be dropped: there is no way to resynchronize framing on a corrupted stream.

## Reading across segment boundaries

`PipeReader.ReadAsync` returns a `ReadOnlySequence<byte>` — a logical buffer that may be backed by **multiple** non-contiguous memory segments (this happens when the pipe's internal ring buffer wraps). A single frame can be split across segment boundaries in either part:

```
segment A: [ ... 0xAC ]      ← first byte of VarInt(300)
segment B: [ 0x02 <payload> ] ← second byte of VarInt + payload
```

`PacketFrameScanner` uses `SequenceReader<byte>` (from `System.Buffers`) internally, which transparently walks across segment boundaries. The VarInt and payload are read correctly even when split — no copying, no allocation.

## PacketFrameScanner

`PacketFrameScanner` is a `ref struct` enumerator that implements the framing scheme above.

### Design choices

- **`ref struct`** — stack-only, zero allocation, cannot be boxed. Matches the project's DOD / GC-free convention.
- **One-shot per `ReadAsync`** — bound to a single `ReadOnlySequence<byte>`. The buffer is invalidated after `AdvanceTo`, so a fresh scanner is created on every read.
- **Status enum, not exceptions** — partial reads are normal; throwing on them would allocate in the hot path. `VarInt.ReadStatus` distinguishes the outcomes.

### API

```csharp
public ref struct PacketFrameScanner
{
    // Default Minecraft frame size limit (~2 MB).
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    public PacketFrameScanner(ReadOnlySequence<byte> input, int maxPacketSize = DEFAULT_MAX_PACKET_SIZE);

    // Advance to the next complete frame.
    // true  → a frame is available in Current.
    // false → inspect Status for the reason.
    public bool MoveNext();

    // Payload of the current frame (valid only after MoveNext() returns true).
    public ReadOnlySequence<byte> Current { get; }

    // Reason for the last MoveNext() returning false:
    //   Complete  → all frames consumed, end of buffered data.
    //   Partial   → incomplete frame, need more data.
    //   Malformed → corrupted frame, drop the connection.
    public VarInt.ReadStatus Status { get; }

    // Position to feed to PipeReader.AdvanceTo(consumed, examined).
    // On Partial points at the START of the incomplete frame,
    // so its bytes stay buffered for the next read.
    public SequencePosition ConsumedPosition { get; }

    // Supports foreach over complete frames.
    public PacketFrameScanner GetEnumerator() => this;
}
```

### Usage in the read loop

```csharp
ReadResult result = await reader.ReadAsync(token);
var scanner = new PacketFrameScanner(result.Buffer);

while (scanner.MoveNext())
{
    ReadOnlySequence<byte> payload = scanner.Current;
    // dispatch payload (e.g. parse packet ID + fields)
}

// Two-arg AdvanceTo: consumed = where the scanner stopped,
// examined = end of buffer (signals "I looked at everything").
reader.AdvanceTo(scanner.ConsumedPosition, result.Buffer.End);

switch (scanner.Status)
{
    case VarInt.ReadStatus.Partial:
        // incomplete frame buffered; loop back to ReadAsync for more
        break;
    case VarInt.ReadStatus.Malformed:
        // drop the connection
        break;
}
```

See `src/Verstack.Protocol/PacketFrameScanner.cs`.
