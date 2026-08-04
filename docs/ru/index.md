# Документация Verstack

Точка входа в документацию Verstack. Каждый документ посвящён одной теме.

## Содержание

 | Документ                          | Описание                                                                                              |
 |-----------------------------------|-------------------------------------------------------------------------------------------------------|
 | [Архитектура](architecture.md)    | Слои, зоны ответственности, граф зависимостей, ECS-миры и проектные решения.                          |
 | [Network](network/index.md)       | Пассивный насос байт: `TcpNetworkService`, `NetworkChannel`, фрейминг, конвейер бандлов.              |
 | [Bridge](bridge/index.md)         | Мост: async-сеть ↔ sync-ECS, конечный автомат сущности, передача владения каналом между слоями.       |
 | [Gateway](gateway/index.md)       | Входной слой: Handshake, Status, Login, Configuration. Бандлы, конвейер, гостевой скрининг, handoff.  |
 | [Realm](realm/index.md)           | Фаза Play: вход в мир, отправка чанков, маршрутизатор входящих play-пакетов.                          |
 | [Engine.World](engine-world/index.md) | Модель чанков, сериализация в wire-формат протокола 26.2, flat-генератор.                         |
 | [Global](global/index.md)         | Глобальный мир: MOTD, кэш ServerInfo, каталог реестров 26.2, DTO для Bridge, ServerTime.              |
 | [NBT](nbt/index.md)               | NBT writer+reader: `NbtWriter`/`NbtReader` (ref struct, `Span<byte>`), modified UTF-8, networked-root.|

## Языки

- [English](../en/index.md)
