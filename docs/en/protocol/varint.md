# VarInt — Variable-Length Integer
> `src/Verstack.Protocol/VarInt.cs`

Used throughout the Minecraft protocol: packet lengths, packet IDs, and other numeric fields.  
Implements **LEB128** (Little-Endian Base 128) encoding.

## Why variable length

Small numbers dominate the protocol. A fixed 4-byte `int32` would waste space.  
VarInt uses 1 byte for `0..127` and up to 5 bytes for large or negative values.

## Bit layout of a single byte

```
bit:    7  6  5  4  3  2  1  0
      ┌───┬─────────────────────┐
      │ C │      data (7)       │
      └───┴─────────────────────┘
        ↑
       continuation: 1 = "more bytes follow", 0 = "last byte"
```

- **Bit 7** — continuation flag. `1` means another byte follows.
- **Bits 0–6** — payload data.

Data is stored **little-endian**: the first byte holds the least significant 7 bits, the next byte holds bits 7–13, and so on.

## Worked example: encoding 300

Split 300 into 7-bit chunks, least-significant first:

```
300 = 0x12C = 1_00101100  (9 bits)
       split into 7-bit groups (LSB first):
       ┌─────────┬─────────┐
       │ 0101100 │   010   │   ← chunks (low first)
       │  =44    │   =2    │
       └─────────┴─────────┘

add continuation bits:
  byte 0: 1 | 0101100 = 0xAC   ← "more follow"
  byte 1: 0 | 0000010 = 0x02   ← "last"

result: [0xAC, 0x02]
```

Decoding reverses this — each byte's 7 bits are shifted into place:

```
byte 0 = 0xAC → continuation set    → data = 0xAC & 0x7F = 44  (bits 0..6)
byte 1 = 0x02 → continuation clear  → data = 0x02 & 0x7F = 2   (bits 7..13)

value = 44 | (2 << 7) = 44 | 256 = 300  ✓
```

The masks in constant use: `0x80` (continuation), `0x7F` (data), and a shift of 7 per byte.

## Edge cases

| Value | Encoding | Why |
|-------|----------|-----|
| `0` | `[0x00]` | One byte, no continuation. |
| `127` | `[0x7F]` | Maximum that fits in a single byte. |
| `128` | `[0x80, 0x01]` | Doesn't fit in 7 bits — needs a second. |
| `-1` | `[0xFF, 0xFF, 0xFF, 0xFF, 0x0F]` | Signed int32 → all 5 bytes. |
| `int.MaxValue` | 5 bytes | Limit. The 5th byte carries only 4 bits (bits 28–31). |

The 5-byte limit comes from the 32-bit signed integer size.  
If the continuation bit is still set after the 5th byte, the data is considered malformed (`Malformed`).

## API surface

### Constants and types

- `MaxSize` — maximum encoded size (5 bytes).
- `ReadStatus` — enum with read outcomes:
  - `Complete` — value successfully decoded.
  - `Partial` — not enough bytes (need more data).
  - `Malformed` — continuation bit set on the 5th byte (invalid data).

### Methods

- `int GetByteCount(int value)` — returns the number of bytes required to encode `value` (does not write).
- `int Encode(int value, Span<byte> destination)` — encodes `value` into the destination buffer. Returns the number of bytes written.
- `bool TryDecode(ReadOnlySpan<byte> source, out int value, out int bytesConsumed)` — decodes a VarInt from a contiguous span of bytes. Returns `false` on partial or corrupt data.
- `ReadStatus TryRead(ref SequenceReader<byte> reader, out int value)` — decodes a VarInt from a `SequenceReader<byte>`, advancing it. Used by `PacketFrameReader` and `PacketPayloadReader` for zero-copy decoding over fragmented buffers.

→ [Protocol layer](index.md)