using Verstack.Shared.Voxel;

namespace Verstack.Shared.Voxel.Tests;

/// <summary>
/// Тесты <see cref="BiomeContainer"/>: пороги BPE для биомов отличаются от блоков
/// (1–3 вместо 4–8), ёмкость 64 вместо 4096. Эти тесты фиксируют различие.
/// </summary>
public class BiomeContainerTests
{
    [Fact]
    public void Constructor_SingleValued_AllEntriesReturnInitial()
    {
        var c = new BiomeContainer(FlatBlockStates.BIOME_PLAINS);

        Assert.Equal(0, c.BitsPerEntry);
        Assert.Equal(FlatBlockStates.BIOME_PLAINS, c.Get(0));
        Assert.Equal(FlatBlockStates.BIOME_PLAINS, c.Get(63));
    }

    /// <summary>
    /// Главное отличие от блоков: биомы стартуют indirect с BPE=1, а не 4.
    /// Это канон протокола 776 (см. .zcode/protocol-776-data-types.md, BPE biomes 1–3).
    /// </summary>
    [Fact]
    public void Set_SecondBiome_GrowsToBpe1_Not4()
    {
        var c = new BiomeContainer(FlatBlockStates.BIOME_PLAINS);

        c.Set(0, 1); // второй биом

        Assert.Equal(1, c.BitsPerEntry); // не 4!
        Assert.Equal(2, c.PaletteSize);
        Assert.Equal(1, c.Get(0));
        Assert.Equal(FlatBlockStates.BIOME_PLAINS, c.Get(63));
    }

    /// <summary>
    /// 5 разных биомов → BPE=3 (ёмкость 8 при BPE=3). Проверка роста 1→2→3.
    /// </summary>
    [Fact]
    public void Set_FiveDistinct_GrowsThroughBpeThresholds()
    {
        var c = new BiomeContainer(0);
        for (int i = 1; i <= 4; i++)
            c.Set(i, i);

        // Палитра: 5 элементов → не влезает в BPE=2 (ёмкость 4) → BPE=3.
        Assert.Equal(3, c.BitsPerEntry);
        Assert.Equal(0, c.Get(0));
        for (int i = 1; i <= 4; i++)
            Assert.Equal(i, c.Get(i));
    }

    /// <summary>
    /// Все 64 ячейки roundtrip — проверка, что ёмкость именно 64, а не 4096.
    /// </summary>
    [Fact]
    public void Set_AllBiomeSlots_Roundtrip()
    {
        var c = new BiomeContainer(0);
        for (int i = 0; i < VoxelMetrics.SECTION_BIOME_COUNT; i++)
            c.Set(i, (i % 7) + 1);

        for (int i = 0; i < VoxelMetrics.SECTION_BIOME_COUNT; i++)
            Assert.Equal((i % 7) + 1, c.Get(i));
    }
}