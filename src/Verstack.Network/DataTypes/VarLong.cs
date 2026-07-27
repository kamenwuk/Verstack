using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Network.DataTypes;

public static class VarLong
{
    public const int MAX_SIZE = 10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Read(ref SequenceReader<byte> reader)
    {
        long value = 0;
        int shift = 0;
        byte b;

        do
        {
            if (!reader.TryRead(out b))
                throw new EndOfStreamException("Не удалось прочитать VarLong: достигнут конца потока.");

            value |= (long)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0 && shift < 70);

        if ((b & 0x80) != 0)
            throw new InvalidDataException("VarLong слишком большой.");

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(IBufferWriter<byte> writer, long value)
    {
        Span<byte> span = writer.GetSpan(MAX_SIZE);
        int written = 0;
        ulong v = (ulong)value;

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
}