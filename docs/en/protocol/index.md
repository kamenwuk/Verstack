# The Protocol layer

Protocol is pure byte logic with no dependencies beyond the base class library. Everything here works on `Span<byte>` and `ReadOnlySequence<byte>` and could run in a console app with no socket — that is the whole point of isolating it. It provides three tools: `VarInt`, the LEB128 integer encoding Minecraft uses for lengths and ids; the framing pair that splits a byte stream into packets and writes them back; and `PacketReader`, which reads fields of a single packet out of an already-framed payload.

`PacketFrameScanner` is the read side — a `ref struct` enumerator that yields complete frames out of a `ReadOnlySequence<byte>`, one-shot per `ReadAsync`. `PacketFraming` is the write side — a `static class` that wraps a payload in a VarInt length prefix and writes a complete frame into an `IBufferWriter<byte>`, in one atomic span. They are mirrors of each other and share `DEFAULT_MAX_PACKET_SIZE`, which lives on `PacketFraming` as the single source of truth.

`PacketReader` sits on top of framing, on the read side: given a whole frame's payload, it reads fields in order — VarInts, big-endian fixed-width numbers, length-prefixed UTF-8 strings, and UUIDs. The UUID is returned as a `Uuid` (not `System.Guid`), defined here in the same layer, which keeps the 128 bits strictly in wire order. This is the abstraction at which per-packet parsers in the Minecraft layer assemble DTOs out of bytes.

→ [VarInt](varint.md)
→ [Packet Framing](packet-framing.md)
→ [Packet Reader](packet-reader.md)
