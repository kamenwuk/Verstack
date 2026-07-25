# Compression — packet compression
> src/Verstack.Protocol/IPacketCompressor.cs

> src/Verstack.Protocol/ZLibPacketCompressor.cs

After the Login phase (Set Compression packet), Minecraft switches the framing to a compressed format (RFC 1950 / zlib). 
The Protocol layer abstracts the compression algorithm via interfaces, allowing the implementation to be swapped (e.g., to P/Invoke a native `zlib-ng`).

## Interfaces
---

**IPacketCompressor**, used by PacketFrameWriter when writing outgoing packets.

``` csharp
 int GetMaxCompressedSize(int sourceLength) — returns an upper bound on the size of the compressed data. Needed so the framework can reserve a contiguous chunk of memory (Span<byte>) in the IBufferWriter before compression begins.
 int Compress(ReadOnlySpan<byte> source, Span<byte> destination) — compresses source into destination, returns the number of bytes written.
```

**IPacketDecompressor**, used by PacketFrameReader when reading incoming packets.

``` csharp
void Decompress(ReadOnlySequence<byte> source, Span<byte> destination) — decompresses data. The size of destination is always exactly equal to dataLength from the frame header.
```

## Default Implementations
---

`ZLibPacketCompressor` and `ZLibPacketDecompressor` use the BCL built-in `System.IO.Compression.ZLibStream`, which calls native `zlib-ng` under the hood.
To minimize allocations (since the Stream API requires `byte[]`), the implementations rent temporary buffers via `ArrayPool<byte>.Shared`.

→ [Protocol layer](index.md)