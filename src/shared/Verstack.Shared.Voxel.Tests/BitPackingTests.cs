using Verstack.Shared.Voxel.Encoding;

namespace Verstack.Shared.Voxel.Tests;

/// <summary>
/// Тесты <see cref="BitPacking"/>: упаковка/чтение записей в long[], формула LongCount
/// (целочисленный ceil с учётом padding между long'ами), устойчивость к старшему биту.
/// </summary>
public class BitPackingTests
{
    // ─────────────────────  LongCount — формула с padding  ─────────────────────

    /// <summary>
    /// Контрольный пример из спецификации: 4096 записей при BPE=5 → 342 long'а, не 320.
    /// Наивная формула (entries*bpe+63)/64 теряет padding и даёт 320 — этот тест ловит регрессию.
    /// </summary>
    [Fact]
    public void LongCount_BPE5_342Longs_Not320()
    {
        Assert.Equal(342, BitPacking.LongCount(VoxelMetrics.SECTION_BLOCK_COUNT, 5));
    }

    [Theory]
    [InlineData(4, 256)]   // 4096/(64/4) = 4096/16 = 256
    [InlineData(5, 342)]   // ceil(4096/(64/5)) = ceil(4096/12) = 342
    [InlineData(8, 512)]   // 4096/(64/8) = 4096/8 = 512
    [InlineData(15, 1024)] // 4096/(64/15) = 4096/4 (floor) = 1024, по 4 записи в long, padding 4 бита
    public void LongCount_SectionBlocks_MatchesExpected(int bpe, int expected)
    {
        Assert.Equal(expected, BitPacking.LongCount(VoxelMetrics.SECTION_BLOCK_COUNT, bpe));
    }

    /// <summary>
    /// Биомы: 64 записи. При BPE=1 — ровно 1 long (64 бита, без padding).
    /// </summary>
    [Fact]
    public void LongCount_Biomes_BPE1_SingleLong()
    {
        Assert.Equal(1, BitPacking.LongCount(VoxelMetrics.SECTION_BIOME_COUNT, 1));
    }

    // ─────────────────────  Roundtrip — записал/прочитал  ─────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(15)]
    public void Roundtrip_AllBlockIndices_ReadsBackWritten(int bpe)
    {
        var longs = new long[BitPacking.LongCount(VoxelMetrics.SECTION_BLOCK_COUNT, bpe)];
        var max = 1 << bpe;

        for (int i = 0; i < VoxelMetrics.SECTION_BLOCK_COUNT; i++)
            BitPacking.Set(longs, i, bpe, i % max);

        for (int i = 0; i < VoxelMetrics.SECTION_BLOCK_COUNT; i++)
            Assert.Equal(i % max, BitPacking.Get(longs, i, bpe));
    }

    /// <summary>
    /// Запись в конкретную ячейку не портит соседние записи того же long'а.
    /// </summary>
    [Fact]
    public void Set_PreservesNeighbours()
    {
        var longs = new long[BitPacking.LongCount(16, 4)]; // 1 long, 16 записей по 4 бита
        for (int i = 0; i < 16; i++)
            BitPacking.Set(longs, i, 4, 0b1010);

        // Перезаписываем 7-ю запись — остальные 15 должны остаться нетронутыми.
        BitPacking.Set(longs, 7, 4, 0b0110);

        for (int i = 0; i < 16; i++)
        {
            var expected = i == 7 ? 0b0110 : 0b1010;
            Assert.Equal(expected, BitPacking.Get(longs, i, 4));
        }
    }

    /// <summary>
    /// Запись со старшим битом long'а выставленным — проверка, что Get использует логический,
    /// а не арифметический сдвиг (тот самый C# trap с ulong).
    /// </summary>
    [Fact]
    public void Get_HighBitSet_UsesLogicalShift()
    {
        var longs = new long[] { -1 }; // все 64 бита выставлены, включая знаковый
        // BPE=1, индекс 63 — старший бит, должен читаться как 1.
        Assert.Equal(1, BitPacking.Get(longs, 63, 1));
        Assert.Equal(1, BitPacking.Get(longs, 0, 1));
    }

    /// <summary>
    /// Некорректный BPE бросает в DEBUG. В Release проверка снимается — тест работает только под Debug.
    /// </summary>
#if DEBUG
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(33)]
    public void EntriesPerLong_InvalidBpe_Throws(int bpe)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitPacking.EntriesPerLong(bpe));
    }
#endif
}