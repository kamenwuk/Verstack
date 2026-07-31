using System.Runtime.CompilerServices;

namespace Verstack.Nbt;

/// <summary>
/// Fluent API (цепочки вызовов) для NbtWriter.
/// Благодаря 'this ref NbtWriter' возвращает ссылку на оригинальный писатель без аллокаций.
/// </summary>
public static class NbtWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter BeginRootCompound(this ref NbtWriter writer)
    {
        writer.BeginRootCompoundInternal();
        return ref writer;
    }
    
    
    // ───────────────────────── Compound ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter BeginCompound(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8)
    {
        writer.WriteNameAndType(NbtTagType.Compound, nameUtf8);
        writer.PushCompoundFrame();
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter BeginCompound(this ref NbtWriter writer)
    {
        writer.OnListItemInternal(NbtTagType.Compound);
        writer.PushCompoundFrame();
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter EndCompound(this ref NbtWriter writer)
    {
        writer.WriteTagType(NbtTagType.End);
        writer.PopFrame();
        return ref writer;
    }

    // ─────────────────────────  List  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter BeginList(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, NbtTagType elementType, int count)
    {
        writer.WriteNameAndType(NbtTagType.List, nameUtf8);
        writer.WriteListHeader(elementType, count);
        writer.PushListFrame(elementType, count);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter BeginList(this ref NbtWriter writer, NbtTagType elementType, int count)
    {
        writer.OnListItemInternal(NbtTagType.List);
        writer.WriteListHeader(elementType, count);
        writer.PushListFrame(elementType, count);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter EndList(this ref NbtWriter writer)
    {
        writer.EndListInternal();
        return ref writer;
    }

    // ───────────────  Скаляры в Compound (с именем)  ───────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteByte(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, sbyte value)
    {
        writer.WriteNameAndType(NbtTagType.Byte, nameUtf8);
        writer.WriteByteRaw((byte)value);
        return ref writer;
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteShort(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, short value)
    {
        writer.WriteNameAndType(NbtTagType.Short, nameUtf8);
        writer.WriteShortRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteInt(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, int value)
    {
        writer.WriteNameAndType(NbtTagType.Int, nameUtf8);
        writer.WriteIntRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteLong(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, long value)
    {
        writer.WriteNameAndType(NbtTagType.Long, nameUtf8);
        writer.WriteLongRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteFloat(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, float value)
    {
        writer.WriteNameAndType(NbtTagType.Float, nameUtf8);
        writer.WriteIntRaw(BitConverter.SingleToInt32Bits(value));
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteDouble(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, double value)
    {
        writer.WriteNameAndType(NbtTagType.Double, nameUtf8);
        writer.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteString(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<byte> valueUtf8)
    {
        writer.WriteNameAndType(NbtTagType.String, nameUtf8);
        writer.WriteStringPayload(valueUtf8);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteBool(this ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, bool value)
    {
        writer.WriteNameAndType(NbtTagType.Byte, nameUtf8);
        writer.WriteByteRaw(value ? (byte)1 : (byte)0);
        return ref writer;
    }


    // ───────────  Скаляры в List (Fluent API)  ───────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemByte(this ref NbtWriter writer, sbyte value)
    {
        writer.OnListItemInternal(NbtTagType.Byte);
        writer.WriteByteRaw((byte)value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemShort(this ref NbtWriter writer, short value)
    {
        writer.OnListItemInternal(NbtTagType.Short); 
        writer.WriteShortRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemInt(this ref NbtWriter writer, int value)
    {
        writer.OnListItemInternal(NbtTagType.Int); 
        writer.WriteIntRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemLong(this ref NbtWriter writer, long value)
    {
        writer.OnListItemInternal(NbtTagType.Long); 
        writer.WriteLongRaw(value);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemFloat(this ref NbtWriter writer, float value)
    {
        writer.OnListItemInternal(NbtTagType.Float); 
        writer.WriteIntRaw(BitConverter.SingleToInt32Bits(value));
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemDouble(this ref NbtWriter writer, double value)
    {
        writer.OnListItemInternal(NbtTagType.Double); 
        writer.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemString(this ref NbtWriter writer, ReadOnlySpan<byte> valueUtf8)
    {
        writer.OnListItemInternal(NbtTagType.String); 
        writer.WriteStringPayload(valueUtf8); 
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref NbtWriter WriteListItemBool(this ref NbtWriter writer, bool value)
    {
        writer.OnListItemInternal(NbtTagType.Byte); 
        writer.WriteByteRaw(value ? (byte)1 : (byte)0);
        return ref writer;
    }
}