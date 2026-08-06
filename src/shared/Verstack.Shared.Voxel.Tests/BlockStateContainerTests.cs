using Verstack.Shared.Voxel;

namespace Verstack.Shared.Voxel.Tests;

/// <summary>
/// Тесты <see cref="BlockStateContainer"/>: single-valued по умолчанию, рост палитры,
/// сохранение значений при переходе single→indirect, пороги BPE (4–8) для блоков.
/// </summary>
public class BlockStateContainerTests
{
    // ─────────────────────  Single-valued  ─────────────────────

    [Fact]
    public void Constructor_SingleValued_AllEntriesReturnInitial()
    {
        var c = new BlockStateContainer(FlatBlockStates.STONE);

        Assert.Equal(0, c.BitsPerEntry);
        Assert.Equal(1, c.PaletteSize);
        Assert.Equal(FlatBlockStates.STONE, c.Get(0));
        Assert.Equal(FlatBlockStates.STONE, c.Get(4095));
    }

    // ─────────────────────  Рост палитры single → indirect  ─────────────────────

    /// <summary>
    /// Главная ловушка: при появлении второго значения старые записи (исходный stone)
    /// должны сохраниться. Если бы Grow забыл, что нули = palette 0, тест упал бы на Get(4095).
    /// </summary>
    [Fact]
    public void Set_SecondValue_PreservesExistingEntries()
    {
        var c = new BlockStateContainer(FlatBlockStates.STONE);

        c.Set(0, FlatBlockStates.DIRT);

        Assert.Equal(4, c.BitsPerEntry);          // single → indirect(4)
        Assert.Equal(2, c.PaletteSize);
        Assert.Equal(FlatBlockStates.DIRT, c.Get(0));
        Assert.Equal(FlatBlockStates.STONE, c.Get(1));
        Assert.Equal(FlatBlockStates.STONE, c.Get(4095));
    }

    [Fact]
    public void Set_SameValue_DoesNotGrowPalette()
    {
        var c = new BlockStateContainer(FlatBlockStates.STONE);

        c.Set(0, FlatBlockStates.STONE); // то же значение, что и в палитре

        Assert.Equal(0, c.BitsPerEntry);
        Assert.Equal(1, c.PaletteSize);
    }

    // ─────────────────────  Рост BPE при большом числе состояний  ─────────────────────

    /// <summary>
    /// 17 разных значений → палитра перестаёт влезать в BPE=4 (ёмкость 16) → рост до 5.
    /// </summary>
    [Fact]
    public void Set_SeventeenDistinct_TriggersBpeGrowth()
    {
        var c = new BlockStateContainer(FlatBlockStates.AIR);
        var states = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 85 };

        for (int i = 0; i < states.Length; i++)
            c.Set(i, states[i]);

        Assert.Equal(5, c.BitsPerEntry);
        for (int i = 0; i < states.Length; i++)
            Assert.Equal(states[i], c.Get(i));
    }

    /// <summary>
    /// Переупаковка indirect→indirect (BPE 4→5) не теряет записей: проверка полного roundtrip.
    /// </summary>
    [Fact]
    public void Set_TriggeringGrowth_RoundtripsAllEntries()
    {
        var c = new BlockStateContainer(FlatBlockStates.AIR);
        // Заполняем случайными id в диапазоне 1..85 паттерном.
        for (int i = 0; i < VoxelMetrics.SECTION_BLOCK_COUNT; i++)
            c.Set(i, (i % 17) + 1);

        for (int i = 0; i < VoxelMetrics.SECTION_BLOCK_COUNT; i++)
            Assert.Equal((i % 17) + 1, c.Get(i));
    }

    // ─────────────────────  Доступ к палитре (для сериализации)  ─────────────────────

    [Fact]
    public void GetPaletteEntry_ReturnsStateIdByIndex()
    {
        var c = new BlockStateContainer(FlatBlockStates.STONE);
        c.Set(0, FlatBlockStates.DIRT);

        // Палитра: [0]=stone, [1]=dirt (порядок добавления).
        Assert.Equal(FlatBlockStates.STONE, c.GetPaletteEntry(0));
        Assert.Equal(FlatBlockStates.DIRT, c.GetPaletteEntry(1));
    }
}