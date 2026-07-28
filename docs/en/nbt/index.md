# The NBT module

NBT (Named Binary Tag) is Minecraft's binary format for structured data: block entities, items, chunk metadata, Registry Data. `Verstack.NBT` is a foundational module at the same level as Network's DataTypes, only as a separate project: 0 NuGet, BCL only.

Both the writer and the reader are implemented — GC-free `ref struct`s with symmetric APIs. The writer emits NBT from the server (Registry Data, future block entities/items in Play); the reader parses NBT from dumps and streams (loading the vanilla datapack for `Verstack.Vanilla`, future NBT from the client). The DOM model (`NbtCompound`/`NbtList` nodes) is deferred — a streaming reader covers the tasks without a node tree. The foundation's goal is to cover Play: block entities, items, chunks.

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

The context (Compound or List) decides how a tag lies in the stream — and both the writer and the reader agree on this:

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

`ModifiedUtf8` is an `internal static class`: `GetByteCount(string)` counts bytes (not characters), `Write(string, Span<byte>)` encodes, `Read(ReadOnlySpan<byte>, Span<char>, out int charsWritten)` decodes back (symmetric with `Write`, including `\0` and surrogates). All three methods are zero-alloc: `Read` writes into the caller's `Span<char>` instead of allocating a `string`. ASCII characters (the dominant case for NBT tag names) take a fast branch with no overhead (widen byte→char); vectorization (AVX2/SSE2, as in ObsidianMC) is deferred — the scalar paths are enough. The destination buffer is reserved at `source.Length` (the maximum: char count ≤ byte count). An implementation detail of `NbtWriter`/`NbtReader`, not part of the public API.

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

## The reader — `NbtReader`

A GC-free reader over `ReadOnlySpan<byte>`, a full mirror of `NbtWriter`: the same fields (`_buffer`, `_frames`, `_networked`, `_offset`, `_depth`), the same `NbtFrame` stack. One constructor for the hot path:

```csharp
Span<NbtFrame> frames = stackalloc NbtFrame[8];
var r = new NbtReader(sourceBuffer, frames, networked: true);
```

The API is zero-alloc throughout: no operation allocates. Two traversal modes, both on top of a single core.

**Tag names — zero-copy byte slice.** NBT tag names in Minecraft registries are all ASCII (`"type"`, `"value"`, `"minecraft:chat_type"`); for them modified-UTF-8 equals ASCII byte-per-char. So `ReadTagName` returns the name as a `ReadOnlySpan<byte>` — a slice of raw bytes straight out of the reader's buffer, with no decoding. The caller compares it against a literal via `SequenceEqual("count"u8)`. Non-ASCII names (`"café"u8`) work with the same byte-compare (mUTF-8 == UTF-8 for BMP without `\0`). `\0` in a name and surrogates are not covered — but such names do not exist in NBT.

**String values — a decoder into `Span<char>`.** The values of strings (IDs, textures, localizations, emoji) can be anything; a full mUTF-8 → UTF-16 decoder is needed for them, but writing into the caller's buffer: `ReadString(Span<char> destination, out int charsWritten)`. The buffer is reserved at the source's size (`stackalloc char[source.Length]` — the maximum: char count ≤ byte count).

**Sequental-core** — peek a tag, then read its payload, symmetric to the writer. In a Compound context:

- `EnterRootCompound()` / `ExitCompound()` — the root. `ExitCompound` reads `0x00` (TAG_End) and pops the frame (the writer writes it symmetrically).
- `EnterCompound()` — enter a nested compound (after peek; reads nothing, only pushes a frame).
- `EnterList(out type, out count)` / `ExitList()` — a list (count comes from the header; `ExitList` only validates the remainder and reads nothing — symmetric with `EndList`).
- `ReadTagName(out type, out ReadOnlySpan<byte> utf8Name)` — peek: reads `[type+name]`, leaves the payload untouched. Returns a zero-copy slice of the name. When `type == TAG_End` it returns `End` and **rolls the offset back** by one byte — the End is not consumed; `ExitCompound` will read it.
- `ReadByte/Short/Int/Long/Float/Double/BoolPayload()` — read the payload of a specific type after peek.
- `ReadStringPayload(Span<char>, out int)` — same for strings (via `ModifiedUtf8.Read`).

In a List context — unnamed overloads `ReadByte/Short/Int/Long/Float/Double/Bool()` (no peek; the type is already declared in the List header) and `ReadString(Span<char>, out int)`.

**Lookup** — find a tag by name within a Compound; the primary scenario in `Verstack.Vanilla` (reading vanilla registries):

- `TryReadByte/Short/Int/Long/Float/Double/Bool(ReadOnlySpan<byte> nameUtf8, out value)` — scan forward to the name, skipping non-matching tags via `SkipPayload`. Name comparison is byte-wise (`SequenceEqual`), with no decoding: both the caller and the stream carry mUTF-8 bytes. Returns `false` if the name is not found.
- `TryReadString(ReadOnlySpan<byte> nameUtf8, Span<char> destination, out int charsWritten)` — same for strings; the value is decoded into destination.
- `TryEnterCompound(ReadOnlySpan<byte> nameUtf8)` / `TryEnterList(ReadOnlySpan<byte> nameUtf8, out type, out count)` — same, for containers.
- `SkipRemaining()` — skip all remaining tags of the Compound up to (but not including) TAG_End. Handy after lookups: the fields of interest are read, the rest does not matter — skip and close.

Invariant: lookup goes **forward only, no rewind**. If a name is found, a second lookup of the same name returns `false` (the caller has already advanced past it). If a field is missing from the stream (common with evolving schemas), it does not break the compound: a false-lookup leaves the cursor on TAG_End, and you can either keep looking up or exit via `ExitCompound`. A single miss does not close the compound — critical for reading vanilla datapacks of different versions.

### Why a `ref struct`, not a `sealed class` — shared by writer and reader

Same reasons as `NbtWriter`: `NbtReader` holds a `ReadOnlySpan<byte>` (a ref struct) over the caller's buffer, so it must itself be a `ref struct`. Its lifetime is bounded by the stack frame — it cannot be stored in a field or passed into a lambda. In tests this forces a 4-line boilerplate per case (you cannot close over `r` in `Assert.Throws<T>(() => r.X())` — manual try/catch).

## Context frames — `NbtFrame`

`NbtFrame` is a `public struct` (visible to the caller so it can pass `Span<NbtFrame>` into the writer constructor; for the caller it is an opaque buffer). Three fields: `Container` (Compound/List), `ExpectedListItem` (for a List — the element type from the header), `ListRemaining` (how many elements the header still expects). The writer keeps the frame array as a stack and mutates the top frame through `ref`.

## Arrays — `NbtWriterArrayExtensions` / `NbtReaderArrayExtensions`

ByteArray/IntArray/LongArray live in an `internal static class` of extension methods (`this ref NbtWriter` / `this ref NbtReader`), so the core holds only the scalar API. Arrays are needed by chunks and Registries (Play); they are not needed for basic testing against reference bytes — hence the separate files. The extensions see the `internal` helpers (`WriteNameAndType`, `OnListItem`, `ReadIntRaw`, `ReadSpan`, etc.) — same assembly, which is why the raw methods are lifted from `private` to `internal`.

Endianness asymmetry on the reader side: a ByteArray is returned as a zero-copy `ReadOnlySpan<byte>` (a byte is indivisible, endian does not matter, the slice points into the reader's buffer). IntArray/LongArray require a BE→host conversion, so the caller supplies a destination `Span<int>`/`Span<long>`, and the reader fills it; the destination size must be ≥ the number of elements in the stream, otherwise a DEBUG exception.

## Validation

Structural validation (Compound/List context, buffer/stack overflow, List type mismatch, string length ≤ 32767 bytes, a stray Exit, lookup outside a Compound) lives only in `#if DEBUG`, through `[Conditional("DEBUG")]` methods that the JIT strips in Release. On the hot path both the writer and the reader trust the caller. Exceptions are `InvalidOperationException` with the `$"[{nameof(NbtWriter)}] ..."` / `$"[{nameof(NbtReader)}] ..."` prefix.

The reader has a second class of errors: **reading past the buffer end** (a corrupt stream, a real EOF). This is not misuse but damaged data — so an `EndOfStreamException` is thrown unconditionally (not only in DEBUG), through explicit checks in the raw reads. Symmetry with both neighboring layers: just as `NbtWriter` trusts the caller in Release, the DataTypes of Network throw `EndOfStreamException` on EOF.

## Current limitations

- **The DOM is deferred.** Only a streaming writer/reader, no node tree. That suffices for Registry Data and chunks; a DOM will be needed only if NBT structures must be mutated in place.
- **`Span<byte>` only.** An `IBufferWriter<byte>` overload (like Network's DataTypes) will be added when NBT is needed on the hot path with fragmented writes.
- **`ModifiedUtf8` vectorization is deferred.** The scalar path (with a fast ASCII detector and a widen byte→char step) is enough for tag names and identifiers; AVX2/SSE2 will come once long-string reads become a hot path.
- **Zero-alloc reader.** Every `NbtReader` operation is allocation-free: names as byte slices, string values into `Span<char>`, scalars via out-parameters. Benchmarks (`Verstack.NBT.Benchmark`) confirm `Allocated: 0` across all cases (re-verified after the optimization).
- **It unblocks Registry Data in Gateway.** The Registry Data packet (S→C 0x07) needs NBT — the writer and the reader are now ready for both modes: listing-only (empty bodies) and full-content (full bodies from `Verstack.Vanilla`). See [Gateway](../gateway/index.md).
- **`Verstack.Vanilla` is the next task.** The reader is laid out to parse the vanilla datapack and assemble full-content Registry Data blobs; the 26.2 data store project is not implemented yet.
