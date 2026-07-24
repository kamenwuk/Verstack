# VarInt — Variable-Length Integer

VarInt is the variable-length integer encoding used throughout the Minecraft protocol — for packet lengths, packet IDs, and other numeric fields. It is identical to **LEB128** (Little-Endian Base 128).

## Why variable length

Small numbers are common (packet lengths, IDs). A fixed 4-byte `int32` would waste space for values that fit in a single byte. VarInt uses 1 byte for `0..127`, scaling up to 5 bytes only for very large or negative values.

## Bit layout of a single byte

```
bit:    7  6  5  4  3  2  1  0
      ┌───┬─────────────────────┐
      │ C │      data (7)       │
      └───┴─────────────────────┘
        ↑
       continuation: 1 = "more bytes follow", 0 = "last byte"
```

- **Bit 7** — continuation. Set → another byte follows this one.
- **Bits 0–6** — 7 bits of payload.

Data is **little-endian**: the first byte carries the **least significant** 7 bits, the next carries bits 7–13, and so on.

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

The last row is why the decoder caps at 5 bytes: if continuation is still set after the 5th byte, the data is malformed and the decode fails.

## API surface

VarInt lives in `Verstack.Protocol` as a static class with:

- `GetByteCount(int)` — bytes needed to encode a value (without writing).
- `Encode(int, Span<byte>)` — writes the value, returns bytes written.
- `TryDecode(ReadOnlySpan<byte>, out int, out int)` — reads a value; returns `false` on partial/corrupt data instead of throwing (partial reads are normal in streaming I/O).

See `src/Verstack.Protocol/VarInt.cs`.