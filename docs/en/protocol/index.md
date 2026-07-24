# The Protocol layer

Protocol is pure byte logic with no dependencies beyond the base class library. Everything here works on `Span<byte>` and `ReadOnlySequence<byte>` and could run in a console app with no socket — that is the whole point of isolating it. It provides two things: `VarInt`, the LEB128 integer encoding Minecraft uses for lengths and ids, and the framing pair that splits a byte stream into packets and writes them back.

`PacketFrameScanner` is the read side — a `ref struct` enumerator that yields complete frames out of a `ReadOnlySequence<byte>`, one-shot per `ReadAsync`. `PacketFraming` is the write side — a `static class` that wraps a payload in a VarInt length prefix and writes a complete frame into an `IBufferWriter<byte>`, in one atomic span. They are mirrors of each other and share `DEFAULT_MAX_PACKET_SIZE`, which lives on `PacketFraming` as the single source of truth.

→ [VarInt](varint.md)
→ [Packet Framing](packet-framing.md)
