# Слой Engine.World

Игровой движок мира: модель чанков Minecraft и их сериализация в сетевые пакеты протокола 26.2 (1.21.x). Используется только из [Realm](../realm/index.md) — `PlayChunkBundle` генерирует чанки и отправляет их игроку при входе. Не зависит от ECS и слоёв — только от [NBT](../nbt/index.md) (для block entities в будущем) и Network (`PacketStreamWriter`).

Зависимости: `Verstack.NBT`, `Verstack.Network`. `net10.0`, `Nullable enable`.

## Модель чанка

Чанк — вертикальная колонка мира. В Verstack чанк = 24 секции по 16³ блоков (высота -64..+320, как у ванили 1.18+). Все типы — `struct` (value type), живут в contiguous-массивах; namespace `Verstack.Engine.World.Chunks`.

### `ChunkSection`

```csharp
public struct ChunkSection
{
    public short BlockCount;
    public short FluidCount;
    public int[] Blocks;   // плоский массив 4096 block-state-id. Индекс: (y << 8) | (z << 4) | x
}
```

Секция 16×16×16 = 4096 блоков. `Blocks` — плоский массив block-state-id (не палетта на каждый чанк, а прямой индекс в глобальный реестр block states). `SetBlock(x, y, z, blockId)` устанавливает блок с инкрементом/декрементом `BlockCount`.

`Serialize(ref PacketStreamWriter)` — сериализация секции в wire-формат block states протокола 26.2:

- Если секция **однородная** (все 4096 одинаковые) → **Single Value Palette, BPE=0**: пишется `BlockCount`, `bitsPerBlock=0`, палетта из одного значения, `data = пусто`. Минимальный объём.
- Иначе → **Direct Palette, BPE=15**: 4096 блоков пакуются в **1024 long** (4 блока × 15 бит = 60 бит на long). `data` — массив 1024 `long` в little-endian.
- Biomes всегда Single Value Palette (BPE=0, Plains=0) — биомы захардкожены, отдельная палетра не строится.

Это не полноценная palette-компрессия как у ванилы (где палетра подбирается под разнообразие блоков в секции). Выбран самый простой безопасный способ: однородная секция ужимается, смешанная уходит direct. Полноценная palette — будущая задача.

### `Chunk`

```csharp
public struct Chunk
{
    public const int SECTIONS_COUNT = 24;   // 24 секции (высота -64..+320)
    public int X;
    public int Z;
    public ChunkSection[] Sections;
    public long[] MotionBlockingHeightmap;  // 37 long с padding'ом
}
```

`SerializeBody(ref PacketStreamWriter)` — полная сериализация тела чанка в packet `level_chunk_with_light` (S→C `0x2D` в стадии Play): координаты, Heightmaps (только `MOTION_BLOCKING`), массив секций, block entities (`count=0`), и **полный свет**. Свет отправляется предзаполненным (26 масок sky/block по `0x03FFFFFF`, по 26 массивов 2048 байт `0xFF`) — комментарий в коде: «Отправляем ПОЛНЫЙ СВЕТ, чтобы клиент 100% отрендерил чанк». Реальный расчёт света — отдельная будущая задача.

`GetSectionSize`/`GetVarIntSize` — расчёт размера для префикса `dataSize` перед телом.

## Генератор — `FlatGenerator`

`public static class FlatGenerator` — тестовый генератор плоского мира:

```csharp
public const int AIR_ID = 0;
public const int STONE_ID = 1;
public static Chunk Generate(int chunkX, int chunkZ)
```

Инициализирует 24 секции пустыми массивами `Blocks = new int[4096]`, заполняет секцию 8 (Y=64..79) камнем (256 блоков, нижний слой Y=64), считает `MotionBlockingHeightmap` со значением 128. `FillHeightmap` пакует 9 бит на значение (256 записей в 37 long). Реальная генерация мира (noise, биомы, структуры) — будущая задача; `FlatGenerator` нужен, чтобы проверить отправку чанков end-to-end.

## Кэш — `ChunkManager`

```csharp
public class ChunkManager
{
    private readonly Dictionary<long, Chunk> _chunks;
    public ref Chunk GetOrGenerate(int chunkX, int chunkZ);   // zero-alloc через CollectionsMarshal
    public ref Chunk Get(int chunkX, int chunkZ);
    private static long PackCoords(int x, int z) => ((long)x << 32) | (uint)z;
}
```

Кэш загруженных чанков. Ключевой приём — `CollectionsMarshal.GetValueRefOrAddDefault`: возвращает **прямую ссылку на структуру** в словаре (без аллокаций, без копирования `Chunk`). При промахе кэша (запись `default`) вызывает `FlatGenerator.Generate` прямо в слот и возвращает `ref`. Так `GetOrGenerate` zero-allocation для уже загруженных чанков и однократная аллокация для новых. Точка расширения: замена делегата генерации на реальный worldgen или загрузку сохранений.

## Текущие ограничения

- **Только direct/single-value palette.** Полноценная palette-компрессия (indirect palette с подбором BPE под разнообразие блоков) не реализована — смешанные секции всегда уходят BPE=15.
- **Свет предзаполненный.** Полный свет (`0xFF`) отправляется заглушкой, реальный расчёт skylight/blocklight — будущая задача.
- **Только flat-генератор.** Реальная генерация мира (noise, биомы, структуры), загрузка/сохранение регионов (Anvil) — не реализованы.
- **Biomes захардкожены.** Single Value Palette Plains (0), без реального распределения биомов по секциям.
- **Только отправка.** Мутации мира от клиента (block breaking/placing) не обрабатываются — модель чанков только читается и сериализуется.
