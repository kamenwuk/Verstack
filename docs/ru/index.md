# Документация Verstack

Точка входа в документацию Verstack. Каждый документ посвящён одной теме.

## Содержание

 | Документ                       | Описание                                         |
 |--------------------------------|--------------------------------------------------|
 | [Архитектура](architecture.md) | Слои, зоны ответственности и проектные решения.  |
 | [Network](network/index.md)    | Слой Network: `TcpServer`, `SessionLifetime`, цикл чтения, швы `IPacketHandler`/`IPacketHandlerFactory`. |
 | [Protocol](protocol/index.md)  | Слой Protocol: `VarInt`, пара фрейминга (`PacketFrameScanner`, `PacketFraming`), `PacketReader`. |
 | [Minecraft](minecraft/index.md) | Слой Minecraft: фазы протокола, диспетчер, DTO, сериализаторы и парсеры. |

> Документы будут добавляться по мере роста проекта (заметки по протоколу, руководство для контрибьюторов и т.д.).

## Языки

- [English](../en/index.md)
