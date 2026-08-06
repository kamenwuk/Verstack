using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

public static class VoxelMetrics
{
    /// <summary>Размер секции/колонки по горизонтали в блоках.</summary>
    public const int SECTION_SIZE = 16;

    /// <summary>Число блоков в одной секции (16³).</summary>
    public const int SECTION_BLOCK_COUNT = SECTION_SIZE * SECTION_SIZE * SECTION_SIZE; // 4096

    /// <summary>Число биом-ячеек в одной секции (4×4×4).</summary>
    public const int SECTION_BIOME_COUNT = 4 * 4 * 4; // 64

    /// <summary>Минимальный Y блока в мире (включительно).</summary>
    public const int MIN_Y = -64;

    /// <summary>Максимальный Y блока в мире (включительно).</summary>
    public const int MAX_Y = 319;

    /// <summary>Высота мира в блоках.</summary>
    public const int WORLD_HEIGHT = MAX_Y - MIN_Y + 1; // 384

    /// <summary>Число секций в колонке (WORLD_HEIGHT / 16).</summary>
    public const int SECTION_COUNT = WORLD_HEIGHT / SECTION_SIZE; // 24

    /// <summary>Площадь колонки в блоках (16×16).</summary>
    public const int COLUMN_AREA = SECTION_SIZE * SECTION_SIZE; // 256

    // --- Индексация блоков ---

    /// <summary>Мировой (x,y,z) → индекс секции в колонке [0..SECTION_COUNT).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SectionIndex(int worldY)
        => (worldY - MIN_Y) / SECTION_SIZE;

    /// <summary>Мировой Y → локальный Y в секции [0..16).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LocalY(int worldY)
        => (worldY - MIN_Y) & (SECTION_SIZE - 1);
    
    /// <summary>Мировой X → локальный X в чанке/секции [0..16).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LocalX(int worldX)
        => worldX & (SECTION_SIZE - 1);

    /// <summary>Мировой Z → локальный Z в чанке/секции [0..16).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LocalZ(int worldZ)
        => worldZ & (SECTION_SIZE - 1);

    /// <summary>Локальные (x,y,z) секции → плоский индекс блока [0..4096).
    /// Порядок: x быстрее всего, затем z, затем y (как в wire-формате Data Array).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BlockIndex(int localX, int localY, int localZ)
        => localY * COLUMN_AREA + localZ * SECTION_SIZE + localX;

    // --- Индексация биомов ---

    /// <summary>Локальные (x,y,z) секции → индекс биом-ячейки [0..64).
    /// Биомная сетка грубее блочной в 4 раза по каждой оси (4×4×4 на секцию).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BiomeIndex(int localX, int localY, int localZ)
        => (localY >> 2) * 16 + (localZ >> 2) * 4 + (localX >> 2);
}