# Verstack Documentation

Entry point for the Verstack documentation. Each document covers one topic.

## Contents

 | Document                        | Description                                            |
 |---------------------------------|--------------------------------------------------------|
 | [Architecture](architecture.md) | Layers, responsibilities, and design decisions.        |
 | [Network](network/index.md)     | The Network layer: `TcpServer`, `SessionLifetime`, the read loop, the `IPacketHandler`/`IPacketHandlerFactory` seams. |
 | [Protocol](protocol/index.md)   | The Protocol layer: `VarInt`, the framing pair (`PacketFrameScanner`, `PacketFraming`), `PacketReader`. |
 | [Minecraft](minecraft/index.md) | The Minecraft layer: protocol phases, the dispatcher, DTOs, serializers, and parsers. |

> More documents will be added as the project grows (protocol notes, contributing guide, etc.).

## Languages

- [Русский](../ru/index.md)