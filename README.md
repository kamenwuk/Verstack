# Verstack

[Русский](README.ru.md)

A Minecraft server implementation written in C# on .NET 10, built from scratch with a focus on readability and performance.

> ⚠️ **Status: early development.** Not playable yet. The project currently targets **Minecraft 26.2** (protocol 776).

## Documentation

Architecture, subsystems, and code conventions live in the [Wiki](docs/en/index.md).

## Build & run

Requirements: **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Verstack.App
```

## Tech stack

| Area    | Choice                                              |
|---------|-----------------------------------------------------|
| Runtime | .NET 10 (LTS)                                       |
| Network | `System.Net.Sockets` + `System.IO.Pipelines`    |
| ECS     | `Leopotam.EcsProto` (+QoL) — DOD, GC-free  |

## Third-party licenses

- **[Leopotam.EcsProto](https://github.com/Leopotam/EcsProto)** (+QoL) — vendored under [MIT-ZARYA](src/engine/Verstack.Engine.Ecs/LICENSE.md). MIT-ZARYA permits use, modification, and redistribution, with one condition: if the software is localized into multiple languages, **a Russian localization is mandatory** and must be no less complete than any other. Verstack meets this — `docs/ru/` and `README.ru.md` mirror the English ones.

## License

[Apache-2.0](LICENSE)
