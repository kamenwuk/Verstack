using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Network.DataTypes;

public static class VarInt
{
    public const int MAX_SIZE = 5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Read(ref SequenceReader<byte> reader)
    {
        int value = 0;
        int shift = 0;
        byte b;

        do
        {
            if (!reader.TryRead(out b))
                throw new EndOfStreamException("Не удалось прочитать VarInt: достигнут конец потока.");

            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0 && shift < 35);

        if ((b & 0x80) != 0)
            throw new InvalidDataException("VarInt слишком большой.");

        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryRead(ref SequenceReader<byte> reader, out int value)
    {
        value = 0;
        int shift = 0;
        byte b;

        do
        {
            if (!reader.TryRead(out b))
                return false; // Данные закончились, читаем в следующий раз

            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0 && shift < 35);

        if ((b & 0x80) != 0)
            return false; // VarLong или битые данные

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(IBufferWriter<byte> writer, int value)
    {
        Span<byte> span = writer.GetSpan(MAX_SIZE);
        int written = 0;
        uint v = (uint)value;

        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0)
                temp |= 0x80;

            span[written++] = temp;
        } while (v != 0);

        writer.Advance(written);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write(Span<byte> span, int value)
    {
        int written = 0;
        uint v = (uint)value;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            span[written++] = temp;
        } while (v != 0);
        return written;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref Packet.SpanWriter writer, int value)
    {
        Span<byte> span = writer.GetSpan(MAX_SIZE);
        int written = 0;
        uint v = (uint)value;

        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0)
                temp |= 0x80;

            span[written++] = temp;
        } while (v != 0);

        writer.Advance(written);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSize(int value)
    {
        if ((value & (0xFFFFFFFF << 7)) == 0) return 1;
        if ((value & (0xFFFFFFFF << 14)) == 0) return 2;
        if ((value & (0xFFFFFFFF << 21)) == 0) return 3;
        if ((value & (0xFFFFFFFF << 28)) == 0) return 4;
        return 5;
    }
}