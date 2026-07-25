# PacketFrameReader — Frame Reader
> `src/Verstack.Protocol/PacketFrameReader.cs`

Reads VarInt-length-prefixed frames from a byte sequence.
Solves the framing problem over a TCP stream: splits a continuous byte stream into individual Minecraft packets.

Supports both foreach iteration and reading compressed frames (zlib).

## Compression
---

If an IPacketDecompressor is passed to the constructor, the reader expects each frame to contain VarInt(dataLength) after the outer length.

- If dataLength == 0: the reader yields the uncompressed payload as-is (0 allocations).
- If dataLength > 0: the reader rents a buffer via ArrayPool<byte>.Shared.Rent(dataLength), decompresses the data into it, and yields a ReadOnlySequence<byte> pointing to this buffer.

> Important: Because the reader can rent memory, it implements IDisposable. The calling code (e.g., SessionLifetime) must use a using block or call Dispose() to return the buffer to the pool.

| Status| Meaning|
|--------|----------|
| `Complete` | Frame successfully read (MoveNext() returned true). |
| `Partial`  | Not enough data — wait for the next ReadAsync. |
| `Malformed`| Continuation bit set on the 5th byte, length exceeds limit, or decompression error. Drop the connection. |


## API
---

```csharp
public ref struct PacketFrameReader : IDisposable
{
    public PacketFrameReader(ReadOnlySequence<byte> input, 
        int maxPacketSize = PacketFrameWriter.DefaultMaxPacketSize, 
        IPacketDecompressor? decompressor = null);

    public bool MoveNext();
    public ReadOnlySequence<byte> Current { get; }
    public VarInt.ReadStatus Status { get; }
    public SequencePosition ConsumedPosition { get; }
    public PacketFrameReader GetEnumerator();
    public void Dispose();
}
```

> Current — valid only after MoveNext() returns true.

> Status — reason when MoveNext() returns false.

> ConsumedPosition — position to pass to PipeReader.AdvanceTo. When Partial, points to the start of the incomplete frame so its bytes remain buffered.

> GetEnumerator() — enables foreach (var frame in reader).

→ [Слой Protocol](index.md)