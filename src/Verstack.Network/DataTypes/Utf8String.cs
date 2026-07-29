using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text;

namespace Verstack.Network.DataTypes;

public static class Utf8String
{
    private const int MAX_STRING_LENGTH = 32767 * 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Read(ref SequenceReader<byte> reader)
    {
        int length = VarInt.Read(ref reader);
        if (length is < 0 or > MAX_STRING_LENGTH)
            throw new InvalidDataException($"Недопустимая длина строки: {length}");

        if (!reader.TryReadExact(length, out ReadOnlySequence<byte> bytes))
            throw new EndOfStreamException("Не удалось прочитать строку: достигнут конца потока.");

        if (bytes.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(bytes.FirstSpan);
        }
        else
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                bytes.CopyTo(buffer);
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(IBufferWriter<byte> writer, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        
        VarInt.Write(writer, byteCount);
        
        Span<byte> span = writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        writer.Advance(byteCount);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref Packet.SpanWriter writer, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);

        VarInt.Write(ref writer, byteCount);

        Encoding.UTF8.GetBytes(value, writer.GetSpan(byteCount));
        writer.Advance(byteCount);
    }
    
    /// <summary>
    /// Записывает предзакодированную UTF-8 строку как VarInt-префикс длины + сырые байты.
    /// GC-free: строка уже закодирована в UTF-8, перекодирование через <see cref="Encoding"/> не выполняется.
    /// Для ASCII-идентификаторов (например, <c>minecraft:...</c>) эквивалент строковой перегрузки,
    /// но без аллокации и подсчёта <see cref="Encoding"/>.GetByteCount на каждом вызове.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref Packet.SpanWriter writer, ReadOnlySpan<byte> value)
    {
        VarInt.Write(ref writer, value.Length);
        value.CopyTo(writer.GetSpan(value.Length));
        writer.Advance(value.Length);
    }
}