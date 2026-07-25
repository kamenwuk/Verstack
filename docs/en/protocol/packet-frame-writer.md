# PacketFrameWriter — Frame Writer
> `src/Verstack.Protocol/PacketFrameWriter.cs`

Writes VarInt-length-prefixed frames to an `IBufferWriter<byte>`.
The counterpart of `PacketFrameReader`: wraps a payload with its length prefix and appends the complete frame.

## API
---

```csharp
public static class PacketFrameWriter
{
    public const int DefaultMaxPacketSize = 2 * 1024 * 1024;

    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload);
}
```

> DefaultMaxPacketSize — standard frame size limit (2 MB). Shared with PacketFrameReader.

> Encode — writes [VarInt(length)][payload] to output in a single contiguous span.

No status enum: writes to a buffer always succeed (or throw on oversized payload).

→ [Protocol layer](index.md)