using Verstack.Shared.Voxel;

namespace Verstack.Shared.Voxel.Tests;

/// <summary>
/// Тесты <see cref="VoxelMetrics"/>: геометрические константы и индексация блоков/биомов.
/// </summary>
public class VoxelMetricsTests
{
    [Fact]
    public void WorldGeometry_MatchesOverworld()
    {
        Assert.Equal(-64, VoxelMetrics.MIN_Y);
        Assert.Equal(319, VoxelMetrics.MAX_Y);
        Assert.Equal(384, VoxelMetrics.WORLD_HEIGHT);
        Assert.Equal(24, VoxelMetrics.SECTION_COUNT);
        Assert.Equal(4096, VoxelMetrics.SECTION_BLOCK_COUNT);
        Assert.Equal(64, VoxelMetrics.SECTION_BIOME_COUNT);
    }

    // ─────────────────────  SectionIndex / LocalY  ─────────────────────

    [Theory]
    [InlineData(-64, 0)]   // самый нижний блок → секция 0
    [InlineData(-48, 1)]   // начало секции 1
    [InlineData(-1, 3)]    // верх секции 3
    [InlineData(0, 4)]     // начало секции 4
    [InlineData(319, 23)]  // самый верхний блок → последняя секция
    public void SectionIndex_Boundaries(int worldY, int expectedSection)
    {
        Assert.Equal(expectedSection, VoxelMetrics.SectionIndex(worldY));
    }

    [Theory]
    [InlineData(-64, 0)]
    [InlineData(-63, 1)]
    [InlineData(-49, 15)]
    [InlineData(319, 15)]
    public void LocalY_WrapsInSection(int worldY, int expectedLocal)
    {
        Assert.Equal(expectedLocal, VoxelMetrics.LocalY(worldY));
    }

    // ─────────────────────  BlockIndex — порядок x,z,y  ─────────────────────

    /// <summary>
    /// Порядок обхода: x быстрее всего, затем z, затем y (как в wire-формате Data Array).
    /// (0,0,0)=0, (1,0,0)=1, ..., (15,0,0)=15, (0,1,0)=16 — переход z.
    /// </summary>
    [Fact]
    public void BlockIndex_XFastest_ThenZ_ThenY()
    {
        Assert.Equal(0, VoxelMetrics.BlockIndex(0, 0, 0));
        Assert.Equal(1, VoxelMetrics.BlockIndex(1, 0, 0));
        Assert.Equal(15, VoxelMetrics.BlockIndex(15, 0, 0));
        Assert.Equal(16, VoxelMetrics.BlockIndex(0, 0, 1));  // z инкремент
        Assert.Equal(256, VoxelMetrics.BlockIndex(0, 1, 0)); // y инкремент
        Assert.Equal(4095, VoxelMetrics.BlockIndex(15, 15, 15));
    }

    // ─────────────────────  BiomeIndex — сетка 4×4×4  ─────────────────────

    /// <summary>
    /// Биомная сетка грубее блочной в 4 раза по каждой оси: 4 соседних блока делят один биом.
    /// </summary>
    [Fact]
    public void BiomeIndex_GroupsFourBlocksPerAxis()
    {
        // Блоки 0..3 по X → одна биом-ячейка; 4..7 → следующая.
        Assert.Equal(0, VoxelMetrics.BiomeIndex(0, 0, 0));
        Assert.Equal(0, VoxelMetrics.BiomeIndex(3, 0, 0));
        Assert.Equal(1, VoxelMetrics.BiomeIndex(4, 0, 0));
        // Полный диапазон: 4×4×4 = 64 ячейки.
        Assert.Equal(63, VoxelMetrics.BiomeIndex(15, 15, 15));
    }
}