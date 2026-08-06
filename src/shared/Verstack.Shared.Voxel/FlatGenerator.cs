namespace Verstack.Shared.Voxel;

/// <summary>
/// Плоский мир (superflat). Поверхность травы на Y=64 — совпадает с SpawnConstants.SPAWN_BLOCK_Y,
/// игрок встаёт стопами на Y=65. Слои снизу вверх: bedrock(-64) → stone(-63..61) →
/// dirt(62..63) → grass_block(64) → air. Биом везде plains.
///
/// Все чанки идентичны по содержимому (координаты chunkX/Z не влияют) — это позволяет
/// в будущем шарить один сериализованный payload между игроками.
/// </summary>
public sealed class FlatGenerator : IChunkGenerator
{
    /// <summary>Y поверхности (верхний блок grass_block).</summary>
    public const int SURFACE_Y = 64;

    private const int BEDROCK_Y = VoxelMetrics.MIN_Y;        // -64
    private const int DIRT_BOTTOM_Y = 62;
    private const int DIRT_TOP_Y = 63;

    public ChunkColumn Generate(int chunkX, int chunkZ)
    {
        var column = new ChunkColumn();  // дефолт: все секции air + plains

        // Секции 0..7 — однородный камень. Быстрый путь: один конструктор на секцию.
        var stoneSectionEnd = VoxelMetrics.SectionIndex(SURFACE_Y - 1); // секция Y=63 → индекс 7
        for (int i = 0; i <= stoneSectionEnd; i++)
            column.ReplaceSection(i, FlatBlockStates.STONE, FlatBlockStates.BIOME_PLAINS);

        var baseX = chunkX * VoxelMetrics.SECTION_SIZE;
        var baseZ = chunkZ * VoxelMetrics.SECTION_SIZE;

        // bedrock — нижний слой секции 0.
        FillLayer(column, baseX, baseZ, BEDROCK_Y, FlatBlockStates.BEDROCK);

        // dirt — два верхних слоя каменной части (Y=62, 63).
        FillLayer(column, baseX, baseZ, DIRT_BOTTOM_Y, FlatBlockStates.DIRT);
        FillLayer(column, baseX, baseZ, DIRT_TOP_Y, FlatBlockStates.DIRT);

        // grass_block — поверхность. SetBlock обновит heightmap'ы до SURFACE_Y.
        FillLayer(column, baseX, baseZ, SURFACE_Y, FlatBlockStates.GRASS_BLOCK);

        return column;
    }

    /// <summary>Заполнить горизонтальный слой 16×16 одним блоком.</summary>
    private static void FillLayer(ChunkColumn column, int baseX, int baseZ, int worldY, int stateId)
    {
        for (int x = 0; x < VoxelMetrics.SECTION_SIZE; x++)
            for (int z = 0; z < VoxelMetrics.SECTION_SIZE; z++)
                column.SetBlock(baseX + x, worldY, baseZ + z, stateId);
    }
}