using System.Runtime.CompilerServices;

namespace Verstack.Nbt;

/// <summary>
/// Расширения <see cref="NbtReader"/> для массивов NBT (TAG_Byte_Array / TAG_Int_Array / TAG_Long_Array).
/// Симметрия с <see cref="NbtWriterArrayExtensions"/>.
///
/// Wire-формат одинаковый для всех трёх: <c>[Int длина BE][N элементов BE]</c>. В Compound перед
/// payload идёт type-байт и имя (читаются через <see cref="NbtReader.ReadTagName"/>), в List —
/// только payload (через <see cref="NbtReader.OnListScalar"/>).
///
/// <b>Endianness-асимметрия:</b> ByteArray можно вернуть как zero-copy <see cref="ReadOnlySpan{T}"/>
/// (byte — неделимая единица, endian не важен). IntArray/LongArray требуют BE→host-преобразования,
/// поэтому caller предоставляет destination <c>Span&lt;int&gt;</c>/<c>Span&lt;long&gt;</c>, и reader
/// заполняет его. Размер destination обязан быть ≥ количеству элементов в потоке, иначе — исключение.
/// </summary>
internal static class NbtReaderArrayExtensions
{
    // ───────────────  В Compound (через lookup, без peek-ahead)  ───────────────
    //
    // Эти перегрузки — lookup-стиль: caller передаёт имя, extension внутри вызывает TrySeekName.
    // Не нашёл имя → false (End НЕ потребляется). Нашёл, но не тот тип → исключение.

    /// <summary>
    /// Ищет TAG_Byte_Array с именем <paramref name="nameUtf8"/> и возвращает zero-copy срез.
    /// <paramref name="value"/> указывает на внутренний буфер reader'а — живёт, пока жив буфер.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadByteArray(this ref NbtReader reader, ReadOnlySpan<byte> nameUtf8, out ReadOnlySpan<byte> value)
    {
        if (reader.TrySeekName(nameUtf8, NbtTagType.ByteArray))
        {
            int count = reader.ReadIntRaw();
            value = reader.ReadSpan(count);
            return true;
        }
        value = default; return false;
    }

    /// <summary>
    /// Ищет TAG_Int_Array с именем <paramref name="nameUtf8"/> и заполняет <paramref name="destination"/>
    /// (BE → host-endian). Размер destination должен быть ≥ количеству элементов в потоке; возвращает
    /// фактическое число прочитанных элементов.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadIntArray(this ref NbtReader reader, ReadOnlySpan<byte> nameUtf8, Span<int> destination, out int count)
    {
        if (reader.TrySeekName(nameUtf8, NbtTagType.IntArray))
        {
            count = reader.ReadIntRaw();
            ReadIntsPayload(ref reader, count, destination);
            return true;
        }
        count = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadLongArray(this ref NbtReader reader, ReadOnlySpan<byte> nameUtf8, Span<long> destination, out int count)
    {
        if (reader.TrySeekName(nameUtf8, NbtTagType.LongArray))
        {
            count = reader.ReadIntRaw();
            ReadLongsPayload(ref reader, count, destination);
            return true;
        }
        count = 0; return false;
    }

    // ───────────────────  В List (без имени, payload only)  ───────────────────

    /// <summary>
    /// Читает ByteArray как элемент List: учитывает List-элемент (OnListScalar), затем payload.
    /// Возвращает zero-copy срез.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> ReadByteArray(this ref NbtReader reader)
    {
        reader.OnListScalar(NbtTagType.ByteArray);
        int count = reader.ReadIntRaw();
        return reader.ReadSpan(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadIntArray(this ref NbtReader reader, Span<int> destination)
    {
        reader.OnListScalar(NbtTagType.IntArray);
        int count = reader.ReadIntRaw();
        ReadIntsPayload(ref reader, count, destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadLongArray(this ref NbtReader reader, Span<long> destination)
    {
        reader.OnListScalar(NbtTagType.LongArray);
        int count = reader.ReadIntRaw();
        ReadLongsPayload(ref reader, count, destination);
    }

    // ───────────────────  Payload-чтение (общее для Compound/List)  ───────────────────

    /// <summary>
    /// Читает <paramref name="count"/> BE-int в <paramref name="destination"/> (host-endian).
    /// Если destination меньше count → бросает. Лишнее место в destination не трогается.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadIntsPayload(ref NbtReader reader, int count, Span<int> destination)
    {
#if DEBUG
        if (count > destination.Length)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] IntArray в потоке ({count}) больше destination ({destination.Length}).");
#endif
        for (int i = 0; i < count; i++)
            destination[i] = reader.ReadIntRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadLongsPayload(ref NbtReader reader, int count, Span<long> destination)
    {
#if DEBUG
        if (count > destination.Length)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] LongArray в потоке ({count}) больше destination ({destination.Length}).");
#endif
        for (int i = 0; i < count; i++)
            destination[i] = reader.ReadLongRaw();
    }
}