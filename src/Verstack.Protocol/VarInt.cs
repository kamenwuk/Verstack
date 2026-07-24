using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// VarInt (LEB128) — variable-length encoding of an <see cref="int"/>.
/// Up to 5 bytes: each byte carries 7 data bits (0..6) and 1 continuation bit (7).
/// </summary>
public static class VarInt
{
    /// <summary>
    /// Outcome of reading a VarInt from a byte stream.
    /// </summary>
    public enum ReadStatus : byte
    {
        /// <summary>VarInt is fully read.</summary>
        Complete,
        /// <summary>Bytes were not enough to make a whole VarInt — more data is needed.</summary>
        Partial,
        /// <summary>Continuation did not close for <see cref="MAX_SIZE"/> bytes — the data is broken.</summary>
        Malformed
    }

    /// <summary>Maximum encoded size, in bytes.</summary>
    public const int MAX_SIZE = 5;

    private const int CONTINUATION_MASK = 0x80;
    private const int DATA_MASK = 0x7F;
    private const int DATA_BITS = 7;

    /// <summary>
    /// Number of bytes required to encode <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// Pure computation without writing to a buffer — for predictive memory allocation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(int value)
    {
        // Считаем по числу значащих 7-битных групп.
        // Беззнаковый сдвиг: отрицательные дают 32 значащих бита → 5 байт.
        uint uValue = (uint)value;
        int bytes = 1;
        uValue >>= DATA_BITS;
        while (uValue != 0)
        {
            bytes++;
            uValue >>= DATA_BITS;
        }
        return bytes;
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="destination"/>.
    /// </summary>
    /// <returns>Number of bytes written (1..<see cref="MAX_SIZE"/>).</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is empty.
    /// </exception>
    public static int Encode(int value, Span<byte> destination)
    {
        if (destination.Length < 1)
            throw new ArgumentException($"[{nameof(VarInt)}] Destination buffer too small.", nameof(destination));

        uint uValue = (uint)value;
        int i = 0;
        do
        {
            // Если данных > 7 бит — будет continuation, иначе последний байт.
            byte data = (byte)(uValue & DATA_MASK);
            uValue >>= DATA_BITS;
            bool hasMore = uValue != 0;
            if (hasMore)
                destination[i] = (byte)(data | CONTINUATION_MASK);
            else
                destination[i] = data;
            i++;
        } while (uValue != 0 && i < destination.Length);

        return i;
    }

    /// <summary>
    /// Decodes a VarInt from a contiguous <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Byte span to read from</param>
    /// <param name="value">Decoded value (0 on failure)</param>
    /// <param name="bytesConsumed">Bytes consumed by the decoder (0 on failure)</param>
    /// <returns>
    /// <c>true</c> on success; <c>false</c> if data is insufficient (partial VarInt)
    /// or invalid (continuation set on the 5th byte → corrupted data).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is empty.
    /// </exception>
    public static bool TryDecode(ReadOnlySpan<byte> source, out int value, out int bytesConsumed)
    {
        if (source.Length < 1)
            throw new ArgumentException($"[{nameof(VarInt)}] Source buffer is empty.", nameof(source));

        // Копим в локал, коммитим только при успехе — на пути неудачи out остаётся чистым.
        int result = 0;
        int shift = 0;

        for (var idx = 0; idx < source.Length; idx++)
        {
            byte data = source[idx];
            result |= (data & DATA_MASK) << shift;

            if ((data & CONTINUATION_MASK) == 0)
            {
                value = result;
                bytesConsumed = idx + 1;
                return true;
            }

            shift += DATA_BITS;
            if (shift > (MAX_SIZE - 1) * DATA_BITS)
            {
                // 5-й байт, а continuation всё ещё выставлен — данные битые.
                value = 0;
                bytesConsumed = 0;
                return false;
            }
        }

        // Дошли до конца source, continuation не закрыт → частичные данные.
        value = 0;
        bytesConsumed = 0;
        return false;
    }

    /// <summary>
    /// Reads a VarInt from <paramref name="reader"/>, transparently crossing segment boundaries.
    /// </summary>
    /// <param name="reader">Sequence reader (advanced by the number of bytes read)</param>
    /// <param name="value">Decoded value (0 on failure)</param>
    public static ReadStatus TryRead(ref SequenceReader<byte> reader, out int value)
    {
        // Копим в локал, коммитим только при успехе — на пути неудачи out остаётся чистым.
        int result = 0;
        int shift = 0;

        for (int idx = 0; idx < MAX_SIZE; idx++)
        {
            if (!reader.TryRead(out byte data))
            {
                // Не хватило байт до целого VarInt — частичные данные.
                value = 0;
                return ReadStatus.Partial;
            }

            result |= (data & DATA_MASK) << shift;

            if ((data & CONTINUATION_MASK) == 0)
            {
                value = result;
                return ReadStatus.Complete;
            }

            shift += DATA_BITS;
        }

        // Прочитано MAX_SIZE байт, continuation не закрылся — VarInt битый.
        value = 0;
        return ReadStatus.Malformed;
    }
}