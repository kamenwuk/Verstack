# Кодовые конвенции

Каноничные правила кодирования Verstack. Конвенции фиксированы по текущему коду, не по историческим решениям.

## Стиль: DOD и GC-free

Архитектурный стиль — data-oriented design (DOD), ECS-системы над `struct`-компонентами. Следуем конвенциям Leopotam
(аспекты через `ProtoAspectInject`, пулы через `ProtoPool<T>`).

**GC-free горячий путь:**

- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` на методах ридеров/райтеров и горячих путях.
- `ref readonly` / `in` для передачи больших структур без копирования.
- `Span<T>` / `ReadOnlySpan<T>` / `stackalloc` / `unsafe fixed` для работы с памятью.
- `ArrayPool<T>.Shared` для аренды буферов (возврат в `finally` или через `IDisposable`).
- Кэши `byte[]` с dirty-flag (напр. `ServerInfoCacheStore` — JSON перестраивается только когда данные изменились).
- Никакого LINQ и `foreach` по коробочным коллекциям в горячем пути.
- `ref struct` для типов, которые должны жить только на стеке и не упаковываться (`PacketStreamReader/Writer`,
  `NbtReader`, `NbtWriter`, `PacketOutbound`, `ScopedAssetBuffer`).

## Naming

| Элемент | Стиль | Пример |
|---------|-------|--------|
| Типы, методы, свойства, public поля | PascalCase | `NetworkChannel`, `TryDequeueHandoff`, `IncomingPackets` |
| Приватные инстансные поля | `_camelCase` | `_serverTime`, `_isRunning` |
| Локальные переменные, параметры | camelCase | `sleepTime`, `channel` |
| Константы | UPPER_SNAKE | `TICKS_PER_SECOND`, `GLOBAL`, `COMPRESSION_THRESHOLD` |

В вендоре Leopotam сохраняются его конвенции (camelCase для внутренних полей, русские тексты исключений). Код вендора
правится минимально.

## Nullable

Настраивается по `.csproj`, общего `Directory.Build.props` **нет** — каждый проект несёт свои настройки.

Текущее распределение:

- `disable` — `Verstack.Engine.Network`, `Verstack.Engine.Bridge`.
- `enable` — все остальные (`Engine.Ecs`, `Engine.Lifecycle`, слои, `Shared.*`, `App`).

Граница не зафиксирована правилом и может меняться. При работе с проектом ориентируйся на его `.csproj`, а не на список
выше. Причина `disable` в движке — много interop с raw-байтами и API без nullable-аннотаций.

## Защитные проверки

- `#if DEBUG` для проверок, которые снимаются в Release (как в EcsProto). Примеры: валидация стека `NbtWriter`,
  проверка `_isWriting` в `PacketOutbound`, контрактные ассерты в Bridge.
- Защиты, отказ от которых крашнет клиента в продакшене (напр. незакрытый NBT), остаются включёнными всегда — без `#if DEBUG`.
  Пример: `NbtWriter.Finish()`.

## Исключения

Текст исключений — **на русском, без префикса типа**.

```csharp
// Правильно:
throw new InvalidOperationException("Слой gateway не имеет права передавать канал дальше.");

// Неправильно (старый стиль):
throw new InvalidOperationException($"[{nameof(BridgeHandoffRouter)}] Слой gateway не имеет права передавать канал дальше.");
```

Стек вызовов C# и так покажет тип-источник, префикс `[TypeName]` избыточен. Раньше в коде были исключения с префиксом и
английским текстом — они вычищены; новые пишем по канону.

## Комментарии

- **XML `///` на public API — на русском**, описывают «что делает». С `<see cref="..."/>` для ссылок на типы.
- **Строчные `//` объясняют «почему»**, не «что». Тоже на русском.
- Концептуальные тексты (битовые раскладки, wire-форматы, диаграммы) — в `docs/`, не в комментариях.
- В вендоре Leopotam комментарии оригинальные (включая английские), не переписываются.

## DI и сервисы

- Поля систем помечаются `[DI]` (инъекция из текущего мира) или `[DI(ServerWorldScopes.GLOBAL)]` (из именованного чужого
  мира). Инициализируются `AutoInjectModule(true)`.
- Сервисы (`TcpNetworkService`, `ServerTime`, компрессоры) добавляются через `ServerComposer.AddService` или через
  модуль (`NetworkHubModule`) и видны всем мирам.
- Доступ к чужому кэш-стору из бандла/системы: `systems.NamedWorlds()[scope].Aspect<T>()` в `Init`.

## Файлы и структура

- Решение — `Verstack.slnx` (XML-формат .NET 10), в `src/`.
- Тесты и бенчмарки — в `!tests/` и `!benchmark/` рядом с проектом (префикс `!` держит их внизу списка в IDE).
- NuGet только в тестах/бенчмарках (`xunit`, `BenchmarkDotNet`); рантайм — 0 NuGet, только BCL + вендор Leopotam.
- Двуязычные доки: `docs/en/` и `docs/ru/` зеркальны. Markdown правится напрямую.
