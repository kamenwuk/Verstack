using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

/// <summary>
/// Колонка чанка: 24 секции (16³ каждая) + две heightmaps. Это «чанк» в разговорном смысле —
/// 16×16 блоков по горизонтали на всю высоту мира.
///
/// Координаты блока в методах — мировые (worldX/Y/Z). Конверсия в индекс секции и локальные
/// координаты делается через VoxelMetrics. Секции хранятся как массив ChunkSection[24],
/// индекс 0 = самая нижняя (Y=-64..-49). Heightmap'ы MotionBlocking и WorldSurface
/// отправляются в пакете 0x2D.
/// </summary>
public sealed class ChunkColumn
{
    private readonly ChunkSection[] _sections;
    private Heightmap _motionBlocking;
    private Heightmap _worldSurface;

    public Heightmap MotionBlocking => _motionBlocking;
    public Heightmap WorldSurface => _worldSurface;

    /// <summary>Число секций (24 для Overworld).</summary>
    public int SectionCount => _sections.Length;
    
    public ChunkColumn()
    {
        _sections = new ChunkSection[VoxelMetrics.SECTION_COUNT];
        _motionBlocking = new Heightmap();
        _worldSurface = new Heightmap();

        // Массив структур не вызывает конструкторы элементов — инициализируем каждую
        // секцию вручную как воздух + plains. Генератор потом перезапишет нужные блоки.
        for (int i = 0; i < _sections.Length; i++)
            _sections[i] = new ChunkSection(FlatBlockStates.AIR, FlatBlockStates.BIOME_PLAINS);
    }

    /// <summary>State id блока по мировым координатам.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetBlock(int worldX, int worldY, int worldZ)
    {
        ref var section = ref GetSection(worldY);
        return section.GetBlock(
            VoxelMetrics.LocalX(worldX), VoxelMetrics.LocalY(worldY), VoxelMetrics.LocalZ(worldZ));
    }

    /// <summary>Записать блок по мировым координатам. Обновляет heightmap'ы.</summary>
    public void SetBlock(int worldX, int worldY, int worldZ, int stateId)
    {
        ref var section = ref GetSection(worldY);
        section.SetBlock(
            VoxelMetrics.LocalX(worldX), VoxelMetrics.LocalY(worldY), VoxelMetrics.LocalZ(worldZ), stateId);
        UpdateHeightmaps(worldX, worldY, worldZ, stateId);
    }

    /// <summary>Установить биом в ячейку 4×4×4 по мировым координатам.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBiome(int worldX, int worldY, int worldZ, int biomeId)
    {
        ref var section = ref GetSection(worldY);
        section.SetBiome(
            VoxelMetrics.LocalX(worldX), VoxelMetrics.LocalY(worldY), VoxelMetrics.LocalZ(worldZ), biomeId);
    }

    /// <summary>Доступ к секции по порядковому индексу [0..SECTION_COUNT).
    /// Секции идут снизу вверх. Для wire-writer'а и массовых операций.</summary>
    public ref ChunkSection GetSectionByIndex(int index)
        => ref _sections[index];

    /// <summary>
    /// Доступ к секции по мировому Y. Возвращает ref — массив структур мутабелен по индексу,
    /// копий не создаётся.
    /// </summary>
    public ref ChunkSection GetSection(int worldY)
        => ref _sections[VoxelMetrics.SectionIndex(worldY)];
    
    /// <summary>
    /// Заменить секцию целиком на однородную (single-valued). Быстрый путь для генератора:
    /// одна аллокация вместо 4096 SetBlock. Не обновляет heightmap'ы — их задаёт генератор.
    /// </summary>
    public void ReplaceSection(int index, int initialStateId, int initialBiomeId)
        => _sections[index] = new ChunkSection(initialStateId, initialBiomeId);

    // Полный incremental-update heightmap (с учётом удаления блока сверху — прорытая дыра)
    // добавим, когда будут правки игроков. Пока — only-grow: при SetBlock не-air выше
    // текущего top обновляем. Достаточно для плоского мира и первичного наполнения.
    private void UpdateHeightmaps(int worldX, int worldY, int worldZ, int stateId)
    {
        if (stateId == FlatBlockStates.AIR)
            return;

        var x = worldX & 15;
        var z = worldZ & 15;
        var top = _motionBlocking.GetTopY(x, z);
        if (worldY > top)
        {
            _motionBlocking.SetTopY(x, z, worldY);
            _worldSurface.SetTopY(x, z, worldY);
        }
    }
}