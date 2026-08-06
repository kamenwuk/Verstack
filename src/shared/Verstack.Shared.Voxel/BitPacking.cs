using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

/// <summary>
/// Упаковка целых в плотный массив <c>long[]</c> — формат Data Array протокола 776.
///
/// <para>Каждая запись занимает <paramref name="bitsPerEntry"/> бит. Внутри одного
/// <c>long</c> записи идут плотно, начиная с <b>младших</b> бит. Запись <b>не может</b>
/// пересекать границу <c>long</c> — если очередная запись не помещается до 64-го бита,
/// остаток long'а остаётся padding'ом, и запись идёт в начало следующего.</para>
///
/// <para>На проводе <c>long</c>'ы кодируются big-endian, поэтому первый бит первой записи
/// окажется в последнем байте (см. wire-writer в Realm). В памяти же храним native-<c>long[]</c>;</para>
///
/// <para><b>Контрольный пример, почему ceil нельзя упрощать:</b> для 4096 записей и BPE=5
/// корректный ответ — 342 long'а, а наивная формула <c>(entries*bpe + 63) / 64</c> даёт 320.
/// Разница — потерянный padding (12 записей на long вместо «плотной» упаковки).</para>
/// </summary>
public static class BitPacking
{
    /// <summary>
    /// Число записей, помещающихся в один <c>long</c> при данном BPE.
    /// <c>floor(64 / bpe)</c>. BPE=0 недопустим (single-valued не использует Data Array).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EntriesPerLong(int bpe)
    {
#if DEBUG
        if (bpe is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(bpe), bpe,
                "BPE должен быть в [1; 32]. Протокол использует ≤15 (blocks) / ≤7 (biomes).");
#endif
        return 64 / bpe;
    }

    /// <summary>
    /// Число <c>long</c>'ов под <paramref name="entries"/> записей при данном BPE.
    ///
    /// <para>Целочисленный <c>ceil(entries / entriesPerLong)</c>. <b>Нельзя</b> упрощать до
    /// <c>(entries * bpe + 63) / 64</c> — это проигнорирует padding между long'ами
    /// и даст меньший массив (см. контрольный пример в заголовке класса).</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LongCount(int entries, int bpe)
    {
        var epl = EntriesPerLong(bpe);
        return (entries + epl - 1) / epl;
    }

    /// <summary>
    /// Прочитать запись <paramref name="index"/> (0-based) из упакованного массива.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Get(ReadOnlySpan<long> data, int index, int bpe)
    {
        var epl = EntriesPerLong(bpe);
        var longIndex = index / epl;
        var bitIndex = (index % epl) * bpe;
        var mask = (1UL << bpe) - 1UL;
        return (int)(((ulong)data[longIndex] >> bitIndex) & mask);
    }

    /// <summary>
    /// Записать значение <paramref name="value"/> в запись <paramref name="index"/>.
    /// Существующие биты записи очищаются, остальные биты long'а сохраняются.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(Span<long> data, int index, int bpe, int value)
    {
        var epl = EntriesPerLong(bpe);
        var longIndex = index / epl;
        var bitIndex = (index % epl) * bpe;
        var mask = (1UL << bpe) - 1UL;

        var slot = (ulong)data[longIndex];
        slot = (slot & ~(mask << bitIndex)) | ((ulong)value << bitIndex);
        data[longIndex] = (long)slot;
    }
}