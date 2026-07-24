# Документация Verstack

Точка входа в документацию Verstack. Каждый документ посвящён одной теме.

## Содержание

 | Документ                       | Описание                                         |
 |--------------------------------|--------------------------------------------------|
 | [Архитектура](architecture.md) | Слои, зоны ответственности и проектные решения.  |
 | [Network](network/index.md)    | Слой Network: `TcpServer`, `SessionLifetime`, цикл чтения и шов `IPacketHandler`. |
 | [Protocol](protocol/index.md)  | Слой Protocol: `VarInt` и пара фрейминга (`PacketFrameScanner`, `PacketFraming`). |
 | [Minecraft](minecraft/index.md) | Слой Minecraft: DTO пакетов, сериализаторы и handler'ы, организованные по состояниям протокола. |

> Документы будут добавляться по мере роста проекта (заметки по протоколу, руководство для контрибьюторов и т.д.).

## Языки

- [English](../en/index.md)
