using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Массивы NBT (TAG_Byte_Array / TAG_Int_Array / TAG_Long_Array). Wire-формат: <c>[Int-длина BE][N элементов BE]</c>.
/// ByteArray — zero-copy срез; IntArray/LongArray требуют BE→host, caller даёт destination.
/// </summary>
public static class NbtReaderArrayExtensions
{
    extension(ref NbtStreamReader reader)
    {
        /// <summary>Ищет TAG_Byte_Array и возвращает zero-copy срез из буфера.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadByteArray(ReadOnlySpan<byte> nameUtf8, out ReadOnlySpan<byte> value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.ByteArray))
            {
                int count = reader.ReadIntRaw();
                value = reader.ReadSpan(count);
                return true;
            }
            value = default; return false;
        }

        /// <summary>Ищет TAG_Int_Array и заполняет destination (BE→host).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadIntArray(ReadOnlySpan<byte> nameUtf8, Span<int> destination, out int count)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.IntArray))
            {
                count = reader.ReadIntRaw();
                ReadIntsPayload(ref reader, count, destination);
                return true;
            }
            count = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadLongArray(ReadOnlySpan<byte> nameUtf8, Span<long> destination, out int count)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.LongArray))
            {
                count = reader.ReadIntRaw();
                ReadLongsPayload(ref reader, count, destination);
                return true;
            }
            count = 0; return false;
        }

        /// <summary>ByteArray как элемент List (zero-copy).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadByteArray()
        {
            reader.OnListScalar(NbtTagType.ByteArray);
            int count = reader.ReadIntRaw();
            return reader.ReadSpan(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadIntArray(Span<int> destination)
        {
            reader.OnListScalar(NbtTagType.IntArray);
            int count = reader.ReadIntRaw();
            ReadIntsPayload(ref reader, count, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadLongArray(Span<long> destination)
        {
            reader.OnListScalar(NbtTagType.LongArray);
            int count = reader.ReadIntRaw();
            ReadLongsPayload(ref reader, count, destination);
        }
    }

    /// <summary>Читает count BE-int в destination (destination &lt; count → fault в DEBUG).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadIntsPayload(this ref NbtStreamReader reader, int count, Span<int> destination)
    {
#if DEBUG
        if (count > destination.Length)
        {
            reader.Faulted = true;
            return;
        }
#endif
        for (int i = 0; i < count; i++)
            destination[i] = reader.ReadIntRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadLongsPayload(this ref NbtStreamReader reader, int count, Span<long> destination)
    {
#if DEBUG
        if (count > destination.Length)
        {
            reader.Faulted = true;
            return;
        }
#endif
        for (int i = 0; i < count; i++)
            destination[i] = reader.ReadLongRaw();
    }
}