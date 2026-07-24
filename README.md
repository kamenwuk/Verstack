# Verstack

[Русский](README.ru.md)

A Minecraft server implementation written in C# on .NET 10, built from scratch with a focus on readability, performance,
and a clean, data-oriented architecture.

> ⚠️ **Status: early development.** Not playable yet. The project currently targets the latest stable Minecraft
> release (1.21.x).

## Documentation

See the [Wiki](docs/en/index.md).

## Goals

- **Readable first.** No magic, no black boxes. Every layer is small enough to understand in one sitting.
- **Performance without sacrifice.** GC-free hot paths, `Span<T>`/`stackalloc`, pooled buffers. Data-oriented design (
  DOD), not classic OOP hierarchies.
- **Made to be read and extended.** The codebase is small and clear enough to follow end-to-end and change.

## Tech stack

| Area         | Choice                                            |
|--------------|---------------------------------------------------|
| Runtime      | .NET 10 (LTS)                                     |
| Network      | `Pipelines.Sockets.Unofficial` over raw sockets   |
| Async        | `System.IO.Pipelines` (`PipeReader`/`PipeWriter`) |
| Architecture | DOD, ECS-style systems, no deep class hierarchies |

## Project structure

```text
src/
├── Verstack.Network/    TCP layer, connection lifecycle. Knows nothing about Minecraft.
├── Verstack.Protocol/   Minecraft wire format: VarInt, NBT, framing, packet ids. Pure logic.
└── Verstack.App/        Entry point, composition root.
tests/
└── Verstack.Protocol.Tests/   Unit tests for protocol primitives.
```

The **Network / Protocol** split is deliberate: Protocol can be tested in isolation by feeding it `Span<byte>`, with no
socket involved.

## Build & run

Requirements: **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Verstack.App
```

## License

[Apache-2.0](LICENSE)