using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Lookup: поиск тегов по имени внутри Compound (scan только вперёд, без перемотки).
/// </summary>
public static class NbtReaderLookupExtensions
{
    extension(ref NbtStreamReader reader)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadByte(ReadOnlySpan<byte> nameUtf8, out sbyte value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Byte)) { value = (sbyte)reader.ReadByteRaw(); return true; }
            value = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadShort(ReadOnlySpan<byte> nameUtf8, out short value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Short)) { value = reader.ReadShortRaw(); return true; }
            value = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt(ReadOnlySpan<byte> nameUtf8, out int value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Int)) { value = reader.ReadIntRaw(); return true; }
            value = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadLong(ReadOnlySpan<byte> nameUtf8, out long value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Long)) { value = reader.ReadLongRaw(); return true; }
            value = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadFloat(ReadOnlySpan<byte> nameUtf8, out float value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Float)) { value = BitConverter.Int32BitsToSingle(reader.ReadIntRaw()); return true; }
            value = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadDouble(ReadOnlySpan<byte> nameUtf8, out double value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Double)) { value = BitConverter.Int64BitsToDouble(reader.ReadLongRaw()); return true; }
            value = 0; return false;
        }

        /// <summary>Ищет String по имени и декодирует значение в destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadString(ReadOnlySpan<byte> nameUtf8, Span<char> destination, out int charsWritten)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.String)) { reader.ReadStringPayload(destination, out charsWritten); return true; }
            charsWritten = 0; return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadBool(ReadOnlySpan<byte> nameUtf8, out bool value)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Byte)) { value = reader.ReadByteRaw() != 0; return true; }
            value = false; return false;
        }

        /// <summary>Ищет вложенный Compound по имени и входит в него (push frame).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterCompound(ReadOnlySpan<byte> nameUtf8)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.Compound)) { reader.PushCompoundFrame(); return true; }
            return false;
        }

        /// <summary>Ищет List по имени и входит в него (push frame).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterList(ReadOnlySpan<byte> nameUtf8, out NbtTagType elementType, out int count)
        {
            if (reader.TrySeekName(nameUtf8, NbtTagType.List))
            {
                elementType = reader.ReadTagType();
                count = reader.ReadIntRaw();
                reader.PushListFrame(elementType, count);
                return true;
            }
            elementType = default; count = 0; return false;
        }

        /// <summary>Ядро lookup: scan до тега с именем и типом (имя сравнивается побайтово). Не совпало → SkipPayload, конец → false.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TrySeekName(ReadOnlySpan<byte> nameUtf8, NbtTagType expected)
        {
#if DEBUG
            ValidateCompoundContextForLookup(ref reader);
#endif
            while (true)
            {
                reader.ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> currentName);
                if (type == NbtTagType.End)
                    return false;
                if (currentName.SequenceEqual(nameUtf8))
                {
#if DEBUG
                    if (type != expected)
                        throw new InvalidOperationException($"Тег найден, но тип {type} ≠ ожидаемому {expected}.");
#endif
                    return true;
                }
                reader.SkipPayload(type);
            }
        }
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateCompoundContextForLookup(ref NbtStreamReader reader)
    {
        if (reader.Depth == 0)
            throw new InvalidOperationException("Lookup вызван до EnterRootCompound (стек пуст).");
        ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException("Lookup в List-контексте; имена тегов есть только в Compound.");
    }
#endif
}