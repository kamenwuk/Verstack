# Protocol layer

Pure byte-level logic for the Minecraft protocol. No network or I/O dependencies — only `Span<byte>` and `ReadOnlySequence<byte>`. Testable without a socket.

Three tool groups:

- **VarInt** — variable-length integer encoding (LEB128). Used for packet lengths, IDs, and numeric fields.
- **Framing** — `PacketFrameReader` reads frames from a byte stream, `PacketFrameWriter` writes frames into a buffer. Both use `VarInt` and share the `DefaultMaxPacketSize` limit.
- **Payload reading** — `PacketPayloadReader` consumes a complete frame payload and sequentially reads VarInts, big-endian numbers, strings, and UUIDs. UUIDs are represented by the `Uuid` type, which preserves wire byte order.

→ [VarInt](varint.md)  
→ [PacketFrameReader](packet-frame-reader.md)  
→ [PacketFrameWriter](packet-frame-writer.md)  
→ [PacketPayloadReader](packet-payload-reader.md)  
→ [Uuid](uuid.md)