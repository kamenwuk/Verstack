# Verstack Documentation

Entry point for the Verstack documentation. Each document covers one topic.

## Contents

 | Document                        | Description                                            |
 |---------------------------------|--------------------------------------------------------|
 | [Architecture](architecture.md) | Layers, responsibilities, and design decisions.        |
 | [Network](network/index.md)     | The Network layer: `TcpServer`, `SessionLifetime`, the read loop, and the `IPacketHandler` seam. |
 | [Protocol](protocol/index.md)   | The Protocol layer: `VarInt` and the framing pair (`PacketFrameScanner`, `PacketFraming`). |
 | [Minecraft](minecraft/index.md) | The Minecraft layer: packet DTOs, serializers, and handlers, organized by protocol state. |

> More documents will be added as the project grows (protocol notes, contributing guide, etc.).

## Languages

- [Русский](../ru/index.md)