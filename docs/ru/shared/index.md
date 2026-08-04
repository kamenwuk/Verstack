# Shared

Переиспользуемые подсистемы без фазовой логики. Три проекта, не зависят от движка и слоёв (листья графа зависимостей —
см. [architecture.md](../architecture.md#граф-зависимостей)). Используются движком и слоями через `using`.

```text
Verstack.Shared.Assets  ← загрузка скомпилированных бинарных ассетов
Verstack.Shared.Nbt     ← NBT reader/writer (GC-free ref struct)
Verstack.Shared.Debug   ← Logger (i18n-логирование)
```

## Assets

`Verstack.Shared.Assets` — загрузка скомпилированных бинарных ассетов на рантайме. **Владелец ассетов в композиции —
[Global](../layers/global.md)**; слои получают данные только через него.

### Пайплайн данных (compile-time)

Данные готовятся до рантайма утилитой `Verstack.Tools.DataCompiler` (см. [architecture.md](../architecture.md)). Этапы
(`Program.cs`):

1. **Очистка** старых `Input/` и `App/assets/`.
2. **Извлечение** из `server.jar` (если лежит в `Reports/`) через `DataExtractor` — ванильные данные реестров/тегов в JSON.
3. **Компиляция** JSON в бинарники: `RegistryCompiler` (`.registry.json` → `.registry`), `TagCompiler` (`.tags.json` → `.tags`),
   `NbtCompiler` (`.nbt.json`/`.json` → `.nbt`). Результат — в `src/Verstack.App/assets/`.

Форматы путей (`AssetCatalogPaths`): `assets/{Catalog}/{Asset}/{Name}{Ext}`. Каталоги — `WorldGen`/`Registries`/`Tags`;
типы — `DimensionTypes`/`Biomes`/...; расширения — `.nbt`/`.registry`/`.tags`. Файлы ассетов коммитятся и копируются в
output через `<CopyToOutputDirectory>` в `Verstack.App.csproj`.

### Загрузка (runtime)

`AssetSource` (static) — точка доступа. Две модели:

- **Кэшируемые (`CachedAssetBuffer`)** — загружаются один раз (`PreloadCached`), держатся в памяти до явной выгрузки
  (`UnloadCached`). Доступ через `GetCached(...)` → `ReadOnlyMemory<byte>`. Для часто используемых данных.
- **Временные (`ScopedAssetBuffer`, `ref struct`)** — `RentScoped(...)` / `ScopedAssetBuffer.Load(path)`, живут в блоке
  `using`, буфер из `ArrayPool`. Читают файл через `RandomAccess` напрямую (без `FileStream`), дескриптор освобождается
  сразу. Для одноразовых данных (напр. NBT размерности на одно соединение).
- **Батч тегов** — `PreloadTagBatch()` грузит все `.tags` разом при старте сервера (вызывается из `Program.cs` до
  `EntryPoint.Start`); `GetTagBatch()` отдаёт массив `TagBatchEntry(RegistryId, Data)` для пакета Update Tags.

Все буферы — из `ArrayPool<byte>`, не аллоцируются на каждый запрос.

## Nbt

`Verstack.Shared.Nbt` — GC-free чтение/запись NBT (Named Binary Tag), формата данных Minecraft.

- `NbtReader` (`ref struct`) — чтение из `ReadOnlySpan<byte>`, stateful, modified UTF-8. Расширения в
  `NbtReaderArrayExtensions`.
- `NbtWriter` (`ref struct`) — запись прямо в `Span<byte>`, networked-root (безымянный корневой compound). Стек
  вложенности (`NbtFrame[]`, caller'а) определяет, писать ли имя тегу и байт типа: внутри Compound — именованный, внутри
  List — безымянный. Расширения в `Writers/NbtWriter{Extensions,ArrayExtensions,JsonExtensions}`.
- `NbtTagType` (`enum : byte`) — ID тегов по спецификации NBT, значения фиксированы (бинарный протокол).
- `NbtFrame` — кадр стека writer'а (Compound/List), public только чтобы caller мог `stackalloc NbtFrame[N]` (буфер
  кадров на стеке — основа GC-free).

Используется в Configuration (`KnownPacksBundle`: биом plains inline + NBT из ассетов) и в DataCompiler
(компиляция `.nbt.json` → `.nbt`).

## Debug

`Verstack.Shared.Debug` — логирование с i18n.

- `Logger` (static) — `Info`/`Warn`/`Error`/`Debug`, каждый принимает `LogKey` + `params object[] args`. Цветной вывод
  в консоль (цвет по уровню), потокобезопасный (`lock`), timestamp + thread id.
- `LogKey` (`enum`) — типобезопасные ключи сообщений (`ServerStart`, `NetworkNewConnection`, `PacketLoginStart` и т.д.).
- `LogLocale` (static) — словарь шаблонов `LogKey → string` с плейсхолдерами (`{0}`, `{1}`...). Сейчас один словарь
  (русский); формат рассчитан на добавление других локалей.

Используется во всех движке и слоях; `LogKey` — каноничный способ логировать событие без форматирования строки на месте.
