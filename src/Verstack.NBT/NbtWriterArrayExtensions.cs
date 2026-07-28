using System.Runtime.CompilerServices;

namespace Verstack.Nbt;

/// <summary>
/// Расширения <see cref="NbtWriter"/> для массивов NBT (TAG_Byte_Array / TAG_Int_Array / TAG_Long_Array).
/// Вынесены отдельно, чтобы ядро writer'а содержало только скалярный API: массивы нужны chunk'ам и
/// Registries (Play), для базового тестирования эталонными байтами необязательны.
///
/// Wire-формат одинаковый для всех трёх: <c>[Int длина BE][N элементов BE]</c>. В Compound им предшествует
/// type-байт и имя (через <see cref="NbtWriter.WriteNameAndType"/>), в List пишется только payload
/// (тип и количество уже в заголовке List, учёт — через <see cref="NbtWriter.OnListItem"/>).
/// </summary>
internal static class NbtWriterArrayExtensions
{
    // ───────────────  В Compound (с именем)  ───────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByteArray(this ref NbtWriter writer, string name, ReadOnlySpan<byte> value)
    {
        writer.WriteNameAndType(NbtTagType.ByteArray, name);
        writer.WriteIntRaw(value.Length);
        writer.WriteSpan(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteIntArray(this ref NbtWriter writer, string name, ReadOnlySpan<int> value)
    {
        writer.WriteNameAndType(NbtTagType.IntArray, name);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteIntRaw(value[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLongArray(this ref NbtWriter writer, string name, ReadOnlySpan<long> value)
    {
        writer.WriteNameAndType(NbtTagType.LongArray, name);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteLongRaw(value[i]);
    }

    // ───────────────────  В List (без имени)  ───────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByteArray(this ref NbtWriter writer, ReadOnlySpan<byte> value)
    {
        writer.OnListItem(NbtTagType.ByteArray);
        writer.WriteIntRaw(value.Length);
        writer.WriteSpan(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteIntArray(this ref NbtWriter writer, ReadOnlySpan<int> value)
    {
        writer.OnListItem(NbtTagType.IntArray);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteIntRaw(value[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLongArray(this ref NbtWriter writer, ReadOnlySpan<long> value)
    {
        writer.OnListItem(NbtTagType.LongArray);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteLongRaw(value[i]);
    }
}