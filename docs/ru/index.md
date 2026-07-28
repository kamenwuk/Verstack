# Документация Verstack

Точка входа в документацию Verstack. Каждый документ посвящён одной теме.

## Содержание

 | Документ                          | Описание                                                                                              |
 |-----------------------------------|-------------------------------------------------------------------------------------------------------|
 | [Архитектура](architecture.md)    | Слои, зоны ответственности, граф зависимостей, ECS-миры и проектные решения.                          |
 | [Network](network/index.md)       | Пассивный насос байт: `TcpNetworkService`, `NetworkChannel`, фрейминг, развязка потоков через очереди. |
 | [NBT](nbt/index.md)               | NBT writer: `NbtWriter` (ref struct, `Span<byte>`), modified UTF-8, networked-root.                   |
 | [Gateway](gateway/index.md)       | Входной слой: Handshake, Status, Login, Configuration. Бандлы, конвейер, гостевой скрининг.           |
 | [Global](global/index.md)         | Глобальный мир: MOTD, кэш ServerInfo, константы, ServerTime.                                           |

> Realm (фаза Play) — запланирован, без deep-dive на данный момент. Заметки будут добавляться по мере роста проекта.

## Языки

- [English](../en/index.md)
