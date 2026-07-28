# The NBT module

NBT (Named Binary Tag) is Minecraft's binary format for structured data: block entities, items, chunk metadata, Registry Data. `Verstack.NBT` is a foundational module at the same level as Network's DataTypes, only as a separate project: 0 NuGet, BCL only.

Only the writer is implemented so far (the server sends NBT; a reader is added in Play, once NBT from the client needs to be read). The DOM model (`NbtCompound`/`NbtList` nodes) is also deferred — on the Registry Data listing path NBT bodies are not written, and a full DOM would sit idle. The foundation's goal is to cover Play: block entities, items, chunks.

For where this module sits in the dependency graph, see [Architecture](../architecture.md).

## Wire format

Big-endian. The tag type is a single byte:

| ID | Type          | Payload                                                              |
|----|---------------|----------------------------------------------------------------------|
| 0  | TAG_End       | (none) — compound terminator                                         |
| 1  | TAG_Byte      | 1 signed byte                                                        |
| 2  | TAG_Short     | 2 bytes BE, signed                                                   |
| 3  | TAG_Int       | 4 bytes BE, signed                                                   |
| 4  | TAG_Long      | 8 bytes BE, signed                                                   |
| 5  | TAG_Float     | 4 bytes BE, IEEE 754 single                                          |
| 6  | TAG_Double    | 8 bytes BE, IEEE 754 double                                          |
| 7  | TAG_Byte_Array | `Int` (BE, length) + N bytes                                        |
| 8  | TAG_String    | `Short` (BE, length, max 32767) + modified-UTF-8 bytes               |
| 9  | TAG_List      | `Byte` (element type) + `Int` (BE, count) + N elements               |
| 10 | TAG_Compound  | named tags until TAG_End                                             |
| 11 | TAG_Int_Array | `Int` (BE, length) + N×4 bytes BE                                    |
| 12 | TAG_Long_Array | `Int` (BE, length) + N×8 bytes BE                                   |

A tag's name (for named tags) is encoded as a `Short` BE length + modified-UTF-8 bytes — the same encoding as TAG_String.

The write context decides what the writer emits for a tag:

- **In a Compound** — every tag is named: `[type byte][Short name length][modified-UTF-8 name][payload]`.
- **In a List** — every element is unnamed and carries no type byte (type and count are already in the List header): `[payload]`.

### Networked vs disk root

From Configuration/Play (1.20.2+) on, NBT travels over the wire in **networked** format. The root compound's type byte (`0x0A`) is always written; the name field is skipped:

```
Disk:    [0x0A][Short=0 (empty name)][children…][0x00]
Network: [0x0A]                     [children…][0x00]
```

`NbtWriter` writes the networked root by default; the disk format (for tests/cross-checks) is enabled with `networked: false`.

## Modified UTF-8 — `ModifiedUtf8`

NBT strings and names use **Java modified UTF-8**, not plain `Encoding.UTF8`. The bit layout matches UTF-8; the differences are in the edge cases:

- `\0` (U+0000) encodes as `0xC0 0x80` (2 bytes), not a single `0x00` — so a NUL byte never appears in a payload.
- Characters outside the BMP (> U+FFFF) go through a UTF-16 surrogate pair, and each surrogate is written as a separate 3-byte block — 6 bytes per character, not 4.

`ModifiedUtf8` is an `internal static class`: `GetByteCount(string)` counts bytes (not characters), `Write(string, Span<byte>)` writes. ASCII characters (the dominant case for NBT tag names) take a fast branch with no overhead; vectorization (AVX2/SSE2, as in ObsidianMC) is deferred — the scalar path is enough. An implementation detail of `NbtWriter`, not part of the public API.

## The writer — `NbtWriter`

A GC-free writer straight into `Span<byte>`. A stateful `ref struct`: it remembers the nesting context through a stack of `NbtFrame` (allocated by the caller via `stackalloc`) and decides on its own whether to write the name and type byte. One constructor for the hot path:

```csharp
Span<NbtFrame> frames = stackalloc NbtFrame[8];
var w = new NbtWriter(payloadBuffer, frames, networked: true);
```

The API splits symmetrically by context. In a Compound — named overloads:

- `BeginRootCompound()` / `EndCompound()` — the root (opened without a name: networked without a name, disk with an empty one).
- `BeginCompound(name)` / `EndCompound()` — a nested named compound.
- `BeginList(name, elementType, count)` / `EndList()` — a named list.
- `WriteByte/Short/Int/Long/Float/Double/String/Bool(name, value)` — named scalars.

In a List — unnamed overloads (no name, no type byte, the element counter decrements):

- `BeginCompound()` / `BeginList(elementType, count)` — container elements.
- `WriteByte/Short/Int/Long/Float/Double/String/Bool(value)` — scalar elements.

`EndCompound()` is shared by root and nested: it writes `0x00` (TAG_End) and pops the frame. `EndList()` writes nothing (the length is already in the header); it only validates that exactly the declared number of elements were written.

`WrittenSpan` returns the finished NBT payload. All methods are marked `[MethodImpl(AggressiveInlining)]`.

### Why a `ref struct`, not a `sealed class`

`NbtWriter` holds a `Span<byte>` over the caller's buffer (stack or rented from `ArrayPool`). `Span<T>` is a ref struct and cannot live in a class's heap field; so the writer must be a `ref struct`. The price is that the writer cannot be stored in an ECS component field or passed through a delegate/lambda — its lifetime is bounded by the stack frame. This is the deliberate price of GC-free: the writer assembles NBT in a single pass, with no intermediate allocations, and is flushed straight to the channel.

## Context frames — `NbtFrame`

`NbtFrame` is a `public struct` (visible to the caller so it can pass `Span<NbtFrame>` into the writer constructor; for the caller it is an opaque buffer). Three fields: `Container` (Compound/List), `ExpectedListItem` (for a List — the element type from the header), `ListRemaining` (how many elements the header still expects). The writer keeps the frame array as a stack and mutates the top frame through `ref`.

## Arrays — `NbtWriterArrayExtensions`

ByteArray/IntArray/LongArray live in an `internal static class` of extension methods (`this ref NbtWriter`), so the writer's core holds only the scalar API. Arrays are needed by chunks and Registries (Play); they are not needed for basic testing against reference bytes — hence the separate file. The extensions see the writer's `internal` helpers (`WriteNameAndType`, `OnListItem`, `WriteIntRaw`, `WriteSpan`) — same assembly, which is why the raw methods are lifted from `private` to `internal`.

## Validation

Structural validation (Compound/List context, buffer/stack overflow, List type mismatch, string length ≤ 32767 bytes) lives only in `#if DEBUG`, through `[Conditional("DEBUG")]` methods that the JIT strips in Release. On the hot path the writer trusts the caller. Exceptions are `InvalidOperationException` with the `$"[{nameof(NbtWriter)}] ..."` prefix.

## Current limitations

- **The reader is deferred to Play.** The writer is tested by comparing against hardcoded reference bytes (`Verstack.NBT.Tests`); a reader is added once NBT from the client needs to be read.
- **The DOM is deferred.** Only a direct writer, no node tree.
- **`Span<byte>` only.** An `IBufferWriter<byte>` overload (like Network's DataTypes) will be added when NBT is needed on the hot path with fragmented writes.
- **It unblocks Registry Data in Gateway.** The Registry Data packet (S→C 0x07) needs NBT and is not sent yet — see [Gateway](../gateway/index.md). The writer is ready, but Registry Data listing goes listing-only (bodies are omitted), so NBT is not written on the Configuration hot path yet.
