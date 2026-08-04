using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Raw-чтение BE-скаляров, имён и строк — фундамент остальных extensions. При выходе за буфер ставит
/// <c>Faulted</c> и возвращает <c>default</c>.
/// </summary>
internal static class NbtReaderRawExtensions
{
    extension(ref NbtStreamReader reader)
    {
        /// <summary>Продвигает offset на count без копирования. При выходе за буфер — fault.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Advance(int count)
        {
            if (reader.Faulted || (uint)(reader.Offset + count) > (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return;
            }
            reader.Offset += count;
        }

        /// <summary>Откатывает offset назад на count (для rollback TAG_End в ReadTagName).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Rollback(int count)
        {
            if (reader.Faulted) return;
            reader.Offset -= count;
        }

        /// <summary>Читает type-байт тега (единственный cast <c>byte → NbtTagType</c>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NbtTagType ReadTagType()
        {
            if (reader.Faulted || (uint)reader.Offset >= (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return default;
            }
            return (NbtTagType)reader.Buffer[reader.Offset++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte ReadByteRaw()
        {
            if (reader.Faulted || (uint)reader.Offset >= (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return 0;
            }
            return reader.Buffer[reader.Offset++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal short ReadShortRaw()
        {
            if (reader.Faulted || (uint)(reader.Offset + 2) > (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return 0;
            }
            short v = BinaryPrimitives.ReadInt16BigEndian(reader.Buffer[reader.Offset..]);
            reader.Offset += 2;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadIntRaw()
        {
            if (reader.Faulted || (uint)(reader.Offset + 4) > (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return 0;
            }
            int v = BinaryPrimitives.ReadInt32BigEndian(reader.Buffer[reader.Offset..]);
            reader.Offset += 4;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long ReadLongRaw()
        {
            if (reader.Faulted || (uint)(reader.Offset + 8) > (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return 0;
            }
            long v = BinaryPrimitives.ReadInt64BigEndian(reader.Buffer[reader.Offset..]);
            reader.Offset += 8;
            return v;
        }

        /// <summary>Срез непрочитанных байт (для extensions, читающих массивы).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlySpan<byte> ReadSpan(int count)
        {
            if (reader.Faulted || (uint)(reader.Offset + count) > (uint)reader.Buffer.Length)
            {
                reader.Faulted = true;
                return ReadOnlySpan<byte>.Empty;
            }
            ReadOnlySpan<byte> s = reader.Buffer[reader.Offset..(reader.Offset + count)];
            reader.Offset += count;
            return s;
        }

        /// <summary>Имя тега как zero-copy срез modified-UTF-8 байт (для ASCII сравним с литералом побайтово).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlySpan<byte> ReadNameBytes()
        {
            short byteCount = reader.ReadShortRaw();
#if DEBUG
            if (byteCount < 0)
                throw new InvalidOperationException($"Отрицательная длина имени: {byteCount}.");
#endif
            ReadOnlySpan<byte> name = reader.Buffer[reader.Offset..(reader.Offset + byteCount)];
            reader.Offset += byteCount;
            return name;
        }

        /// <summary>Читает payload строки (Short-длина + modified-UTF-8) и декодирует в destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadStringPayload(Span<char> destination, out int charsWritten)
        {
            short byteCount = reader.ReadShortRaw();
#if DEBUG
            if (byteCount < 0)
                throw new InvalidOperationException($"Отрицательная длина строки: {byteCount}.");
#endif
            ModifiedUtf8.Read(reader.Buffer[reader.Offset..(reader.Offset + byteCount)], destination, out charsWritten);
            reader.Offset += byteCount;
        }
    }
}