# Verstack

[English](README.md)

Реализация сервера Minecraft на C# под .NET 10, написанная с нуля с упором на читаемость и производительность.

> ⚠️ **Статус: ранняя разработка.** Пока неиграбельно. Проект нацелен на **Minecraft 26.2** (протокол 776).

## Документация

Архитектура, подсистемы и конвенции кода — в [Wiki](docs/ru/index.md).

## Сборка и запуск

Требования: **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Verstack.App
```

## Технологический стек

| Область | Выбор                                               |
|---------|-----------------------------------------------------|
| Рантайм | .NET 10 (LTS)                                       |
| Сеть    | `System.Net.Sockets` + `System.IO.Pipelines`    |
| ECS     | `Leopotam.EcsProto` (+QoL) — DOD, GC-free |

## Сторонние лицензии

- **[Leopotam.EcsProto](https://github.com/Leopotam/EcsProto)** (+QoL) — завендорен под [MIT-ZARYA](src/engine/Verstack.Engine.Ecs/LICENSE.md). MIT-ZARYA разрешает использование, изменение и распространение с одним условием: если ПО локализовано на несколько языков, **обязательна локализация на Русский язык**, не менее полная, чем на любом другом. Verstack этому соответствует — `docs/ru/` и `README.ru.md` зеркальны английским.

## Лицензия

[Apache-2.0](LICENSE)
