# PacketFrameWriter — Frame Writer
> `src/Verstack.Protocol/PacketFrameWriter.cs`

Writes VarInt-length-prefixed frames to an IBufferWriter<byte>.
The counterpart to PacketFrameReader: wraps a payload with its length and adds the complete frame to the buffer.

Supports two modes:

1. Uncompressed (default): [VarInt(len)][payload].
2. Compressed: enabled if an IPacketCompressor is provided and compressionThreshold >= 0. The frame format changes to [VarInt(packetLength)][VarInt(dataLength)][payload | compressedPayload]

## Compression Logic
---

If compression is enabled, the decision is based on the payload size:

- payload.Length < threshold: the frame is not compressed. dataLength is written as 0, followed by the raw payload.
- payload.Length >= threshold: the frame is compressed via IPacketCompressor. dataLength is written as payload.Length, followed by the compressed buffer.

To estimate the required buffer size, PacketFrameWriter queries the compressor for GetMaxCompressedSize(). It then gets a contiguous Span<byte> from the IBufferWriter and writes headers and data without extra allocations.

## API
---

```csharp
public static class PacketFrameWriter
{
    public const int DefaultMaxPacketSize = 2 * 1024 * 1024;

    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload, 
        IPacketCompressor? compressor = null, int compressionThreshold = -1);
        {
        }
```

> DefaultMaxPacketSize — standard frame size limit (2 MB). Shared with PacketFrameReader.

> Encode — writes the frame to output. If compressor is null or compressionThreshold < 0, compression is not applied.

→ [Protocol layer](index.md)