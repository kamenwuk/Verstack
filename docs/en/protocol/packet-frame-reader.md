# PacketFrameReader — Frame Reader
> `src/Verstack.Protocol/PacketFrameReader.cs`

Reads VarInt-length-prefixed frames from a byte sequence.  
Handles TCP stream framing: splits a continuous flow of bytes into complete Minecraft packets.

Supports `foreach` iteration.

## Why framing is needed
---

TCP delivers a stream of bytes — it has no concept of message boundaries.  
A single read may contain multiple packets, a partial packet, or a fragment of a header.

The VarInt-length-prefix rule marks where one packet ends and the next begins:

```
[ VarInt: payload length ][ payload: N bytes ]
```

`PacketFrameReader` consumes the VarInt, checks whether the full payload is available,
and yields it as a `ReadOnlySequence<byte>`.

See [VarInt](varint.md) for the length encoding.

## API
---

```csharp
public ref struct PacketFrameReader
{
    public PacketFrameReader(ReadOnlySequence<byte> input, int maxPacketSize = PacketFrameWriter.DefaultMaxPacketSize);

    public bool MoveNext();
    public ReadOnlySequence<byte> Current { get; }
    public VarInt.ReadStatus Status { get; }
    public SequencePosition ConsumedPosition { get; }
    public PacketFrameReader GetEnumerator();
}
```

> Current — valid only after MoveNext() returns true.

> Status — reason when MoveNext() returns false.

> ConsumedPosition — position to pass to PipeReader.AdvanceTo. When Partial, points to the start of the incomplete frame so its bytes remain buffered.

> GetEnumerator() — enables foreach (var frame in reader).

→ [Слой Protocol](index.md)