# Verstack Documentation

Entry point to Verstack documentation. Each document covers a single topic; details follow the links, without duplication.

## Languages

- [Русский](../ru/index.md)

## Core

| Document | Description |
|----------|-------------|
| [Architecture](architecture.md) | Project map and dependency direction |
| [Code conventions](conventions.md) | GC-free, ref struct, nullable, exceptions, naming |

## Engine

| Document | Description |
|----------|-------------|
| [Engine](engine/index.md) | The engine: Ecs, Lifecycle, Network — no knowledge of Minecraft phases |
| [Bridge](engine/bridge.md) | Network↔ECS decoupling: channel routing, player state machine |
| [Network](engine/network.md) | Framing, compression, PacketReader/Writer, NetworkChannel |

## Layers

| Document | Description |
|----------|-------------|
| [Layers](layers/index.md) | ECS phase layers: layer entry point, Bundle conveyor |
| [Global](layers/global.md) | GLOBAL world: ServerInfo, SyncedRegistryCatalog, Assets owner |
| [Gateway](layers/gateway.md) | GATEWAY world: Status, Login, Configuration |
| [Realm](layers/realm.md) | REALM world: Play phase (Join, Movement) |

## Shared

| Document | Description |
|----------|-------------|
| [Shared](shared/index.md) | Assets (DataCompiler→binary→App), Nbt, Debug |
