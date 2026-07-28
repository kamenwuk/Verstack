using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Buffers;

namespace Verstack.Network.DataTypes;

public static class Numeric
{
    // --- Short ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadShort(ref SequenceReader<byte> reader)
    {
        return !reader.TryReadBigEndian(out short value) ? throw new EndOfStreamException() : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteShort(IBufferWriter<byte> writer, short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(writer.GetSpan(2), value);
        writer.Advance(2);
    }

    // --- UShort ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUShort(ref SequenceReader<byte> reader)
    {
        if (!reader.TryReadBigEndian(out short value)) throw new EndOfStreamException();
        return (ushort)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUShort(IBufferWriter<byte> writer, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(writer.GetSpan(2), value);
        writer.Advance(2);
    }

    // --- Int ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt(ref SequenceReader<byte> reader)
    {
        return !reader.TryReadBigEndian(out int value) ? throw new EndOfStreamException() : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt(IBufferWriter<byte> writer, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(4), value);
        writer.Advance(4);
    }

    // --- Long ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadLong(ref SequenceReader<byte> reader)
    {
        return !reader.TryReadBigEndian(out long value) ? throw new EndOfStreamException() : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLong(IBufferWriter<byte> writer, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(writer.GetSpan(8), value);
        writer.Advance(8);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLong(ref Packet.SpanWriter writer, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(writer.GetSpan(8), value);
        writer.Advance(8);
    }

    // --- Float ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadFloat(ref SequenceReader<byte> reader)
    {
        return !reader.TryReadBigEndian(out int value) ? throw new EndOfStreamException() : BitConverter.Int32BitsToSingle(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFloat(IBufferWriter<byte> writer, float value)
    {
        BinaryPrimitives.WriteInt32BigEndian(writer.GetSpan(4), BitConverter.SingleToInt32Bits(value));
        writer.Advance(4);
    }

    // --- Double ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDouble(ref SequenceReader<byte> reader)
    {
        return !reader.TryReadBigEndian(out long value) ? throw new EndOfStreamException() : BitConverter.Int64BitsToDouble(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDouble(IBufferWriter<byte> writer, double value)
    {
        BinaryPrimitives.WriteInt64BigEndian(writer.GetSpan(8), BitConverter.DoubleToInt64Bits(value));
        writer.Advance(8);
    }
}