using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Network.DataTypes;

public static class Bool
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Read(ref SequenceReader<byte> reader)
    {
        if (!reader.TryRead(out byte value))
            throw new EndOfStreamException("Не удалось прочитать Boolean.");
        return value != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(IBufferWriter<byte> writer, bool value)
    {
        writer.GetSpan(1)[0] = value ? (byte)1 : (byte)0;
        writer.Advance(1);
    }
}