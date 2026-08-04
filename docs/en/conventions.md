# Code conventions

Canonical coding rules for Verstack. The conventions are fixed by the current code, not by historical decisions.

## Style: DOD and GC-free

The architectural style is data-oriented design (DOD), ECS systems over `struct` components. We follow Leopotam's
conventions (aspects via `ProtoAspectInject`, pools via `ProtoPool<T>`).

**GC-free hot path:**

- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on reader/writer methods and hot paths.
- `ref readonly` / `in` for passing large structs without copying.
- `Span<T>` / `ReadOnlySpan<T>` / `stackalloc` / `unsafe fixed` for memory work.
- `ArrayPool<T>.Shared` for buffer rental (return in `finally` or via `IDisposable`).
- `byte[]` caches with a dirty-flag (e.g. `ServerInfoCacheStore` — the JSON is rebuilt only when data changed).
- No LINQ or `foreach` over boxed collections on the hot path.
- `ref struct` for types that must live only on the stack and not be boxed (`PacketStreamReader/Writer`, `NbtReader`,
  `NbtWriter`, `PacketOutbound`, `ScopedAssetBuffer`).

## Naming

| Element | Style | Example |
|---------|-------|---------|
| Types, methods, properties, public fields | PascalCase | `NetworkChannel`, `TryDequeueHandoff`, `IncomingPackets` |
| Private instance fields | `_camelCase` | `_serverTime`, `_isRunning` |
| Local variables, parameters | camelCase | `sleepTime`, `channel` |
| Constants | UPPER_SNAKE | `TICKS_PER_SECOND`, `GLOBAL`, `COMPRESSION_THRESHOLD` |

Leopotam's own conventions are preserved in the vendor (camelCase for internal fields, Russian exception texts). Vendored
code is modified minimally.

## Nullable

Configured per `.csproj`; there is **no** common `Directory.Build.props` — each project carries its own settings.

Current distribution:

- `disable` — `Verstack.Engine.Network`, `Verstack.Engine.Bridge`.
- `enable` — all others (`Engine.Ecs`, `Engine.Lifecycle`, layers, `Shared.*`, `App`).

The boundary isn't fixed by a rule and may change. When working in a project, orient by its `.csproj`, not by the list
above. The reason for `disable` in the engine is lots of interop with raw bytes and APIs without nullable annotations.

## Defensive checks

- `#if DEBUG` for checks that are stripped in Release (as in EcsProto). Examples: `NbtWriter` stack validation, the
  `_isWriting` check in `PacketOutbound`, contract asserts in Bridge.
- Protections whose absence would crash the client in production (e.g. an unclosed NBT) stay on always — without
  `#if DEBUG`. Example: `NbtWriter.Finish()`.

## Exceptions

Exception text is **in Russian, without a type prefix**.

```csharp
// Correct:
throw new InvalidOperationException("Слой gateway не имеет права передавать канал дальше.");

// Wrong (old style):
throw new InvalidOperationException($"[{nameof(BridgeHandoffRouter)}] Слой gateway не имеет права передавать канал дальше.");
```

The C# call stack already shows the source type; the `[TypeName]` prefix is redundant. Exceptions with a prefix and
English text used to be in the code — they've been cleaned up; new ones follow the canon.

## Comments

- **XML `///` on public API — in Russian**, describing "what it does." With `<see cref="..."/>` for type references.
- **Inline `//` explain "why"**, not "what." Also in Russian.
- Conceptual texts (bit layouts, wire formats, diagrams) — in `docs/`, not in comments.
- In the Leopotam vendor comments are original (including English ones), not rewritten.

## DI and services

- System fields are marked `[DI]` (injection from the current world) or `[DI(ServerWorldScopes.GLOBAL)]` (from a named
  foreign world). Initialized by `AutoInjectModule(true)`.
- Services (`TcpNetworkService`, `ServerTime`, compressors) are added via `ServerComposer.AddService` or via a module
  (`NetworkHubModule`) and are visible to all worlds.
- Access to a foreign cache store from a bundle/system: `systems.NamedWorlds()[scope].Aspect<T>()` in `Init`.

## Files and structure

- The solution is `Verstack.slnx` (.NET 10 XML format), in `src/`.
- Tests and benchmarks live in `!tests/` and `!benchmark/` next to the project (the `!` prefix keeps them at the bottom
  of the IDE list).
- NuGet only in tests/benchmarks (`xunit`, `BenchmarkDotNet`); runtime — 0 NuGet, only BCL + the vendored Leopotam.
- Bilingual docs: `docs/en/` and `docs/ru/` are mirrored. Markdown is edited directly.
