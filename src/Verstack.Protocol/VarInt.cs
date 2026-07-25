using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// VarInt (LEB128) encoding for <see cref="int"/> — up to 5 bytes, 7 data bits per byte, 1 continuation bit.
/// </summary>
public static class VarInt
{
    /// <summary>Result of reading a VarInt from a byte stream.</summary>
    public enum ReadStatus : byte
    {
        /// <summary>VarInt fully decoded.</summary>
        Complete,
        /// <summary>Not enough bytes — more data needed.</summary>
        Partial,
        /// <summary>Continuation bit set on 5th byte — data is corrupt.</summary>
        Malformed
    }

    /// <summary>Maximum encoded size in bytes.</summary>
    public const int MAX_SIZE = 5;

    private const int CONTINUATION_MASK = 0x80;
    private const int DATA_MASK = 0x7F;
    private const int DATA_BITS = 7;

    /// <summary>Returns the number of bytes needed to encode <paramref name="value"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(int value)
    {
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

    /// <summary>Encodes <paramref name="value"/> into <paramref name="destination"/>.</summary>
    /// <returns>Number of bytes written.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is empty.</exception>
    public static int Encode(int value, Span<byte> destination)
    {
        if (destination.Length < 1)
            throw new ArgumentException("Destination buffer too small.", nameof(destination));

        uint uValue = (uint)value;
        int i = 0;
        do
        {
            byte data = (byte)(uValue & DATA_MASK);
            uValue >>= DATA_BITS;
            bool hasMore = uValue != 0;
            destination[i] = (byte)(hasMore ? data | CONTINUATION_MASK : data);
            i++;
        } while (uValue != 0 && i < destination.Length);

        return i;
    }

    /// <summary>Decodes a VarInt from a contiguous byte span.</summary>
    /// <param name="source">Bytes to read from.</param>
    /// <param name="value">Decoded value (0 on failure).</param>
    /// <param name="bytesConsumed">Bytes consumed (0 on failure).</param>
    /// <returns><c>true</c> on success; <c>false</c> if data is incomplete or malformed.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is empty.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> source, out int value, out int bytesConsumed)
    {
        if (source.Length < 1)
            throw new ArgumentException("Source buffer is empty.", nameof(source));

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
                value = 0;
                bytesConsumed = 0;
                return false;
            }
        }

        value = 0;
        bytesConsumed = 0;
        return false;
    }

    /// <summary>Reads a VarInt from a <see cref="SequenceReader{T}"/>, advancing the reader.</summary>
    /// <param name="reader">The sequence reader.</param>
    /// <param name="value">Decoded value (0 on failure).</param>
    /// <returns><see cref="ReadStatus"/> indicating the outcome.</returns>
    public static ReadStatus TryRead(ref SequenceReader<byte> reader, out int value)
    {
        int result = 0;
        int shift = 0;

        for (int idx = 0; idx < MAX_SIZE; idx++)
        {
            if (!reader.TryRead(out byte data))
            {
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

        value = 0;
        return ReadStatus.Malformed;
    }
}