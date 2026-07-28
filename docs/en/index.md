# Verstack Documentation

Entry point to Verstack documentation. Each document covers a single topic.

## Contents

 | Document                          | Description                                                                                            |
 |-----------------------------------|--------------------------------------------------------------------------------------------------------|
 | [Architecture](architecture.md)   | Layers, responsibilities, dependency graph, ECS worlds and design decisions.                           |
 | [Network](network/index.md)       | Passive byte pump: `TcpNetworkService`, `NetworkChannel`, framing, thread/ECS decoupling via queues.   |
 | [NBT](nbt/index.md)               | NBT writer: `NbtWriter` (ref struct, `Span<byte>`), modified UTF-8, networked root.                    |
 | [Gateway](gateway/index.md)       | Entry layer: Handshake, Status, Login, Configuration. Bundles, pipeline, guest screening.              |
 | [Global](global/index.md)         | Global world: MOTD, ServerInfo cache, constants, ServerTime.                                           |

> Realm (Play phase) is planned, without a deep-dive at this time. Notes will be added as the project grows.

## Languages

- [Русский](../ru/index.md)
