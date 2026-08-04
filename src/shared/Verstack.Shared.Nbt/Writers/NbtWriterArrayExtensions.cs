using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>
/// Расширения <see cref="NbtWriter"/> для массивов NBT (TAG_Byte_Array / TAG_Int_Array / TAG_Long_Array).
/// Вынесены отдельно, чтобы ядро writer's содержало только скалярный API.
/// Поддерживает Fluent API (возвращает ref NbtWriter).
/// </summary>
public static class NbtWriterArrayExtensions
{
    // ───────────────  В Compound (с именем)  ───────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteByteArray(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<byte> value)
    {
        writer.WriteNameAndType(NbtTagType.ByteArray, nameUtf8);
        writer.WriteIntRaw(value.Length);
        writer.WriteSpan(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteIntArray(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<int> value)
    {
        writer.WriteNameAndType(NbtTagType.IntArray, nameUtf8);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteIntRaw(value[i]);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteLongArray(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<long> value)
    {
        writer.WriteNameAndType(NbtTagType.LongArray, nameUtf8);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteLongRaw(value[i]);
        return ref writer;
    }

    // ───────────────────  В List (без имени)  ───────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteByteArray(this ref NbtWriter writer, ReadOnlySpan<byte> value)
    {
        writer.OnListItemInternal(NbtTagType.ByteArray);
        writer.WriteIntRaw(value.Length);
        writer.WriteSpan(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteIntArray(this ref NbtWriter writer, ReadOnlySpan<int> value)
    {
        writer.OnListItemInternal(NbtTagType.IntArray);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteIntRaw(value[i]);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteLongArray(this ref NbtWriter writer, ReadOnlySpan<long> value)
    {
        writer.OnListItemInternal(NbtTagType.LongArray);
        writer.WriteIntRaw(value.Length);
        for (var i = 0; i < value.Length; i++)
            writer.WriteLongRaw(value[i]);
        return ref writer;
    }
}