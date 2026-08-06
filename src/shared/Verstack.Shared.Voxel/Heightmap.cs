using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

/// <summary>
/// Heightmap колонки: 256 записей (по одной на колонку 16×16), упакованных в <c>long[]</c>
/// через <see cref="BitPacking"/>. Формат протокола 776.
///
/// <para><b>BPE</b> = <c>ceil(log2(WORLD_HEIGHT + 1))</c>. Для Overworld (384 блока) → 9
/// (2^9=512, влезает 385 значений: 0 = «нет блока», 1..384 = topY+1 с offset'ом).
/// На один long (64 бита) помещается 7 записей (64/9), всего нужно 37 long'ов на колонку.</para>
///
/// <para>Значения хранятся в «мировых» терминах: <see cref="Empty"/> (нет блока) или
/// Y верхнего блока. Конверсия в wire-значение (worldY → worldY - MIN_Y + 1) делается
/// в <see cref="SetTopY"/> и <see cref="GetTopY"/>.</para>
/// </summary>
public struct Heightmap
{
    /// <summary>BPE для Overworld.</summary>
    public const int BPE = 9;

    /// <summary>Число записей (16×16 колонок в чанке).</summary>
    public const int ENTRY_COUNT = VoxelMetrics.COLUMN_AREA; // 256

    /// <summary>«Нет блока» в мировых терминах.</summary>
    public const int EMPTY = -1;

    private long[] _data;

    public Heightmap()
    {
        _data = new long[BitPacking.LongCount(ENTRY_COUNT, BPE)];
    }

    /// <summary>Y верхнего блока колонки (x,z) или <see cref="EMPTY"/>, если блока нет.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetTopY(int x, int z)
    {
        var raw = BitPacking.Get(_data, ColumnIndex(x, z), BPE);
        return raw == 0 ? EMPTY : raw + VoxelMetrics.MIN_Y - 1;
    }

    /// <summary>Записать Y верхнего блока колонки (x,z). <paramref name="worldY"/>=<see cref="EMPTY"/> → «нет блока».</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTopY(int x, int z, int worldY)
    {
        var raw = worldY == EMPTY ? 0 : worldY - VoxelMetrics.MIN_Y + 1;
        BitPacking.Set(_data, ColumnIndex(x, z), BPE, raw);
    }

    /// <summary>Заполнить всю heightmap одним значением (однородная поверхность плоского мира).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(int worldY)
    {
        var raw = worldY == EMPTY ? 0 : worldY - VoxelMetrics.MIN_Y + 1;
        for (int i = 0; i < ENTRY_COUNT; i++)
            BitPacking.Set(_data, i, BPE, raw);
    }

    /// <summary>Доступ к упакованному long[] для сериализации (wire-writer читает этот массив).</summary>
    public ReadOnlySpan<long> RawData => _data;

    /// <summary>Индекс колонки в heightmap: x быстрее всего, затем z.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ColumnIndex(int x, int z) => z * VoxelMetrics.SECTION_SIZE + x;
}