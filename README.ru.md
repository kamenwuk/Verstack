# Verstack

[English](README.md)

Реализация сервера Minecraft на C# под .NET 10, написанная с нуля с упором на читаемость, производительность и чистую
data-oriented архитектуру.

> ⚠️ **Статус: ранняя разработка.** Пока неиграбельно. Проект нацелен на последнюю стабильную версию Minecraft (1.21.x).

## Документация

См. [Wiki](docs/ru/index.md).

## Цели

- **Читаемость прежде всего.** Без магии и чёрных ящиков. Каждый слой достаточно мал, чтобы понять его за один присест.
- **Производительность без компромиссов.** GC-free горячие пути, `Span<T>`/`stackalloc`, переиспользуемые буферы.
  Data-oriented design (DOD), а не классические OOP-иерархии.
- **Читаемый и расширяемый.** Кодовая база достаточно мала и понятна, чтобы проследить её целиком и изменять.

## Технологический стек

| Область          | Выбор                                                           |
|------------------|-----------------------------------------------------------------|
| Среда выполнения | .NET 10 (LTS)                                                   |
| Сеть             | `Pipelines.Sockets.Unofficial` поверх сырых сокетов             |
| Асинхронность    | `System.IO.Pipelines` (`PipeReader`/`PipeWriter`)               |
| Архитектура      | DOD, ECS-подобные системы, без глубоких иерархий классов        |
| ECS              | завендоренный `Leopotam.EcsProto` (+QoL) — DOD, GC-free горячий путь |

## Структура проекта

Три ECS-мира по скоупам — `GLOBAL` (виден всем), `GATEWAY` (входной слой: Handshake/Status/Login/Configuration), `REALM` (фаза Play). Видимость миров и направление зависимостей проектов совпадают: `Layer.Realm → Layer.Gateway → Layer.Global → Core`. Network не знает про Minecraft-фазы; фазовые слои не лезут в сокеты напрямую.

```text
src/
├── Verstack.App/            Точка входа.
├── Verstack.Bootstrap/      Композиция: ServerComposer + главный тик-луп.
├── Verstack.Core/           Базовые абстракции: VerstackFeature, WorldScopes, ServerTime.
├── Verstack.Debug/          Logger (LogKey + LogLocale, i18n-словарь).
├── Verstack.ECS/            Завендоренный Leopotam.EcsProto + QoL. 0 NuGet, только BCL.
├── Verstack.Network/        TCP/сокеты + фрейминг (вкл. сжатие). Пассивный насос байт.
├── Verstack.Layer.Global/   GLOBAL-мир: MOTD, ServerInfo, константы.
├── Verstack.Layer.Gateway/  GATEWAY-мир: Handshake, Status, Login, Configuration.
├── Verstack.Layer.Realm/    REALM-мир: фаза Play (запланирован).
└── Verstack.NBT/            NBT (запланирован).
tools/
└── Verstack.Probe/          Нагрузочный имитатор N клиентов.
```

Фазовые слои полностью построены на ECS: каждая фаза Minecraft — это ECS-системы над миром, а пакеты идут через конвейер из `PacketBundle`'ов. Каждый бандл описывает исходящие пакеты через `PacketOutbound`; фрейминг и сжатие — забота транспорта. Полная карта — в [Архитектуре](docs/ru/architecture.md).

## Сборка и запуск

Требования: **.NET 10 SDK**.

```bash
dotnet build
dotnet run --project src/Verstack.App
```

## Сторонние лицензии

- **[Leopotam.EcsProto](https://github.com/Leopotam/EcsProto)** (+QoL) — завендорен под [MIT-ZARYA](src/Verstack.ECS/LICENSE.md). MIT-ZARYA разрешает использование, изменение и распространение с одним условием: если ПО локализовано на несколько языков, **обязательна локализация на Русский язык**, не менее полная, чем на любом другом. Verstack этому соответствует — `docs/ru/` и `README.ru.md` зеркальны английским.

## Лицензия

[Apache-2.0](LICENSE)