# Shared

Reusable subsystems without phase logic. Three projects, depending on neither the engine nor the layers (graph leaves —
see [architecture.md](../architecture.md#dependency-graph)). Used by the engine and layers via `using`.

```text
Verstack.Shared.Assets  ← loader for compiled binary assets
Verstack.Shared.Nbt     ← NBT reader/writer (GC-free ref struct)
Verstack.Shared.Debug   ← Logger (i18n logging)
```

## Assets

`Verstack.Shared.Assets` — runtime loading of compiled binary assets. **The assets owner in composition is
[Global](../layers/global.md)**; layers get data only through it.

### Data pipeline (compile-time)

Data is prepared ahead of runtime by the `Verstack.Tools.DataCompiler` utility (see [architecture.md](../architecture.md)).
Stages (`Program.cs`):

1. **Cleanup** of old `Input/` and `App/assets/`.
2. **Extraction** from `server.jar` (if present in `Reports/`) via `DataExtractor` — vanilla registry/tag data into JSON.
3. **Compilation** of JSON into binaries: `RegistryCompiler` (`.registry.json` → `.registry`), `TagCompiler`
   (`.tags.json` → `.tags`), `NbtCompiler` (`.nbt.json`/`.json` → `.nbt`). Result lands in `src/Verstack.App/assets/`.

Path formats (`AssetCatalogPaths`): `assets/{Catalog}/{Asset}/{Name}{Ext}`. Catalogs — `WorldGen`/`Registries`/`Tags`;
types — `DimensionTypes`/`Biomes`/...; extensions — `.nbt`/`.registry`/`.tags`. Asset files are committed and copied to
output via `<CopyToOutputDirectory>` in `Verstack.App.csproj`.

### Loading (runtime)

`AssetSource` (static) — the access point. Two models:

- **Cached (`CachedAssetBuffer`)** — loaded once (`PreloadCached`), held in memory until explicit unload
  (`UnloadCached`). Access via `GetCached(...)` → `ReadOnlyMemory<byte>`. For frequently used data.
- **Scoped (`ScopedAssetBuffer`, `ref struct`)** — `RentScoped(...)` / `ScopedAssetBuffer.Load(path)`, lives in a `using`
  block, buffer from `ArrayPool`. Reads the file via `RandomAccess` directly (no `FileStream`); the handle is released
  immediately. For one-shot data (e.g. dimension NBT per connection).
- **Tag batch** — `PreloadTagBatch()` loads all `.tags` at once on server startup (called from `Program.cs` before
  `EntryPoint.Start`); `GetTagBatch()` returns an array of `TagBatchEntry(RegistryId, Data)` for the Update Tags packet.

All buffers come from `ArrayPool<byte>`, not allocated per request.

## Nbt

`Verstack.Shared.Nbt` — GC-free reading/writing of NBT (Named Binary Tag), Minecraft's data format.

- `NbtReader` (`ref struct`) — reading from `ReadOnlySpan<byte>`, stateful, modified UTF-8. Extensions in
  `NbtReaderArrayExtensions`.
- `NbtWriter` (`ref struct`) — writing directly into `Span<byte>`, networked-root (unnamed root compound). The nesting
  stack (`NbtFrame[]`, owned by the caller) decides whether to write the tag's name and type byte: inside Compound —
  named, inside List — unnamed. Extensions in `Writers/NbtWriter{Extensions,ArrayExtensions,JsonExtensions}`.
- `NbtTagType` (`enum : byte`) — tag IDs per the NBT spec, values fixed (binary protocol).
- `NbtFrame` — a writer stack frame (Compound/List), public only so the caller can `stackalloc NbtFrame[N]` (the frame
  buffer on the stack is the basis of GC-free).

Used in Configuration (`KnownPacksBundle`: plains biome inline + NBT from assets) and in DataCompiler
(compiling `.nbt.json` → `.nbt`).

## Debug

`Verstack.Shared.Debug` — logging with i18n.

- `Logger` (static) — `Info`/`Warn`/`Error`/`Debug`, each taking `LogKey` + `params object[] args`. Colored console
  output (color by level), thread-safe (`lock`), timestamp + thread id.
- `LogKey` (`enum`) — type-safe message keys (`ServerStart`, `NetworkNewConnection`, `PacketLoginStart`, etc.).
- `LogLocale` (static) — dictionary of `LogKey → string` templates with placeholders (`{0}`, `{1}`...). Currently one
  dictionary (Russian); the format is designed for adding other locales.

Used across all engine and layers; `LogKey` is the canonical way to log an event without formatting the string on the spot.
