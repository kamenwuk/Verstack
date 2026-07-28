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

| Area         | Choice                                                          |
|--------------|-----------------------------------------------------------------|
| Runtime      | .NET 10 (LTS)                                                   |
| Network      | `Pipelines.Sockets.Unofficial` over raw sockets                 |
| Async        | `System.IO.Pipelines` (`PipeReader`/`PipeWriter`)               |
| Architecture | DOD, ECS-style systems, no deep class hierarchies               |
| ECS          | vendored `Leopotam.EcsProto` (+QoL) — DOD, GC-free hot path     |

## Project structure

Three ECS worlds by scope — `GLOBAL` (visible to all), `GATEWAY` (the entry layer: Handshake/Status/Login/Configuration), `REALM` (the Play phase). Visibility of worlds and project dependencies point the same way: `Layer.Realm → Layer.Gateway → Layer.Global → Core`. Network knows nothing about Minecraft phases; phase layers never touch sockets directly.

```text
src/
├── Verstack.App/            Entry point.
├── Verstack.Bootstrap/      Composition: ServerComposer + the main tick loop.
├── Verstack.Core/           Base abstractions: VerstackFeature, WorldScopes, ServerTime.
├── Verstack.Debug/          Logger (LogKey + LogLocale, i18n dictionary).
├── Verstack.ECS/            Vendored Leopotam.EcsProto + QoL. 0 NuGet, BCL only.
├── Verstack.Network/        TCP/sockets + framing (incl. compression). Passive byte pump.
├── Verstack.Layer.Global/   GLOBAL world: MOTD, ServerInfo, constants.
├── Verstack.Layer.Gateway/  GATEWAY world: Handshake, Status, Login, Configuration.
├── Verstack.Layer.Realm/    REALM world: Play phase (planned).
└── Verstack.NBT/            NBT (planned).
tools/
└── Verstack.Probe/          Load-testing N-client simulator.
```

The phase layers are built entirely on ECS: each Minecraft phase is ECS systems over the world, and packets flow through a conveyor of `PacketBundle`s. Each bundle describes outgoing packets via `PacketOutbound`; framing and compression are the transport's concern. See [Architecture](docs/en/architecture.md) for the full map.

## Build & run

Requirements: **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Verstack.App
```

## Third-party licenses

- **[Leopotam.EcsProto](https://github.com/Leopotam/EcsProto)** (+QoL) — vendored under [MIT-ZARYA](src/Verstack.ECS/LICENSE.md). MIT-ZARYA permits use, modification, and redistribution, with one condition: if the software is localized into multiple languages, **a Russian localization is mandatory** and must be no less complete than any other. Verstack meets this — `docs/ru/` and `README.ru.md` mirror the English ones.

## License

[Apache-2.0](LICENSE)