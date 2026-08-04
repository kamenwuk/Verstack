# Архитектура

Карта проектов Verstack и направление зависимостей. Детали реализации — в deep-dive'ах по группам: [engine](engine/index.md),
[layers](layers/index.md), [shared](shared/index.md). Конвенции кода — отдельно в [conventions.md](conventions.md).

## Группы проектов

Исходники (`src/`) разбиты на четыре группы по роли. Решение собрано в `Verstack.slnx` (XML-формат .NET 10).

**engine/** — движок. Не знает про Minecraft-фазы: сокеты, фрейминг, ECS, композиция сервера, тик-луп.

| Проект | Роль |
|--------|------|
| `Verstack.Engine.Ecs` | Завендоренный `Leopotam.EcsProto` + QoL. 0 NuGet, только BCL |
| `Verstack.Engine.Lifecycle` | Композиция сервера: `ServerFeatureLayer`, `ServerComposer`, `EntryPoint` (тик-луп), `ServerTime`, `ServerWorldScopes`, `ServerConstants` |
| `Verstack.Engine.Network` | TCP/сокеты, фрейминг, компрессия. Пассивный насос байт |
| `Verstack.Engine.Bridge` | Развязка Network↔ECS: маршрутизация каналов между слоями, конечный автомат игрока |

**layers/** — фазовые слои Minecraft на ECS. Каждый видит только Global; связь между Gateway и Realm — через Bridge.

| Проект | Роль |
|--------|------|
| `Verstack.Layers.Global` | GLOBAL-мир: `ServerInfo`, `SyncedRegistryCatalog`, владелец Assets. Виден всем, сам никого не знает |
| `Verstack.Layers.Gateway` | GATEWAY-мир: Status, Login, Configuration. Входной слой |
| `Verstack.Layers.Realm` | REALM-мир: фаза Play (Join, Movement) |

**shared/** — переиспользуемые подсистемы без фазовой логики. Не зависят от движка и слоёв.

| Проект | Роль |
|--------|------|
| `Verstack.Shared.Debug` | `Logger` (`LogKey` + `LogLocale`, i18n-словарь) |
| `Verstack.Shared.Nbt` | NBT reader/writer (`ref struct`, modified UTF-8, networked-root) |
| `Verstack.Shared.Assets` | Загрузка скомпилированных бинарных ассетов (`AssetCatalog`, кэш-буферы) |

**tools/** — утилиты сборки данных, не входят в рантайм.

| Проект | Роль |
|--------|------|
| `Verstack.Tools.DataCompiler` | Компилятор ванильных JSON → бинарные `.registry`/`.tags`/`.nbt` в `App/assets/` |

Тесты и бенчмарки лежат рядом с проектами в `!tests/` и `!benchmark/` (префикс `!` держит их внизу списка в IDE).

## Граф зависимостей

Стрелка `A → B` означает «A ссылается на B».

```text
Verstack.App
  ├─→ Verstack.Engine.Lifecycle
  ├─→ Verstack.Layers.Global
  ├─→ Verstack.Layers.Gateway
  ├─→ Verstack.Layers.Realm
  └─→ Verstack.Shared.Assets

Verstack.Engine.Lifecycle ─→ Verstack.Engine.Bridge
                           └─→ Verstack.Engine.Network
Verstack.Engine.Bridge     ─→ Verstack.Engine.Network
Verstack.Engine.Network    ─→ Verstack.Engine.Ecs
Verstack.Engine.Ecs        ─→  (ничего, только BCL)

Verstack.Layers.Global  ─→ Verstack.Engine.Lifecycle
Verstack.Layers.Gateway ─→ Verstack.Engine.{Bridge, Ecs, Lifecycle, Network}
                         └─→ Verstack.Shared.{Assets, Nbt}
                         └─→ Verstack.Layers.Global
Verstack.Layers.Realm   ─→ Verstack.Engine.Ecs
                         └─→ Verstack.Layers.Global

Verstack.Shared.{Debug, Nbt, Assets} ─→  (ничего, только BCL)
```

Все engine-проекты зависят от `Verstack.Shared.Debug` (логирование). `Shared.*` — листья графа, ни от кого не зависят.

Слои не повторяют единообразно набор engine-зависимостей: каждый берёт ровно то, что нужно. Global — только Lifecycle
(ему не нужен Network и Bridge, он не работает с сокетами). Realm — Engine.Ecs + Global (фазовая логика поверх ECS,
сетевой intake делегирован Bridge, который Realm'у недоступен напрямую).

## Миры и видимость

Три ECS-мира по скоупам — `GLOBAL`, `GATEWAY`, `REALM` (константы в `ServerWorldScopes`). Видимость **плоская**:

- `GLOBAL` виден всем слоям, но сам Global ни про кого не знает.
- `GATEWAY` и `REALM` **друг друга не видят** — ни в ECS, ни в зависимостях проектов.

Связь между Gateway и Realm идёт **только через Bridge** — он передаёт владение каналом игрока от слоя к слою. Как именно
устроен Bridge (роутер, состояния, порядок систем на тике) — в [engine/bridge.md](engine/bridge.md). Как слой объявляет
свой скоуп, следующего и видимость — в [layers/index.md](layers/index.md).

## Тик-луп

`EntryPoint.Start` создаёт `ServerComposer`, собирает из `ServerFeatureLayer`'ов массив `ProtoSystems` (по одному на слой),
вызывает `Init()` на каждом и запускает главный цикл. Каждый тик: по очереди `layer.Run()` на всех слоях, затем
`ServerTime.Update()`, затем сон до конца тика (20 TPS, 50 мс). Останов — по `Ctrl+C` или закрытию консоли.

Композиция миров (видимость, регистрация чужих миров) выполняется в `ServerComposer.Compose` в три фазы: создание миров →
настройка видимости → инициализация слоёв. Детали — в [engine/index.md](engine/index.md).
