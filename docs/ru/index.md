# Документация Verstack

Входная точка в документацию Verstack. Каждый документ описывает одну тему; подробности — по ссылкам, без дублирования.

## Языки

- [English](../en/index.md)

## Основное

| Документ | Описание |
|----------|----------|
| [Архитектура](architecture.md) | Карта проектов и направление зависимостей |
| [Кодовые конвенции](conventions.md) | GC-free, ref struct, nullable, исключения, naming |

## Engine

| Документ | Описание |
|----------|----------|
| [Engine](engine/index.md) | Движок: Ecs, Lifecycle, Network — без знания о Minecraft-фазах |
| [Bridge](engine/bridge.md) | Развязка Network↔ECS: маршрутизация каналов, состояния игрока |
| [Network](engine/network.md) | Фрейминг, компрессия, PacketReader/Writer, NetworkChannel |

## Layers

| Документ | Описание |
|----------|----------|
| [Layers](layers/index.md) | Фазовые слои на ECS: точка входа слоя, Bundle-конвейер |
| [Global](layers/global.md) | GLOBAL-мир: ServerInfo, SyncedRegistryCatalog, владелец Assets |
| [Gateway](layers/gateway.md) | GATEWAY-мир: Status, Login, Configuration |
| [Realm](layers/realm.md) | REALM-мир: фаза Play (Join, Movement) |

## Shared

| Документ | Описание |
|----------|----------|
| [Shared](shared/index.md) | Assets (DataCompiler→binary→App), Nbt, Debug |
