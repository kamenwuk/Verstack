using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>Массивы NBT (TAG_Byte_Array / TAG_Int_Array / TAG_Long_Array). Fluent: возвращает <c>ref NbtStreamWriter</c>.</summary>
public static class NbtWriterArrayExtensions
{
    extension(ref NbtStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteByteArray(ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<byte> value)
        {
            streamWriter.WriteNameAndType(NbtTagType.ByteArray, nameUtf8);
            streamWriter.WriteIntRaw(value.Length);
            streamWriter.WriteSpan(value);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteIntArray(ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<int> value)
        {
            streamWriter.WriteNameAndType(NbtTagType.IntArray, nameUtf8);
            streamWriter.WriteIntRaw(value.Length);
            for (var i = 0; i < value.Length; i++)
                streamWriter.WriteIntRaw(value[i]);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteLongArray(ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<long> value)
        {
            streamWriter.WriteNameAndType(NbtTagType.LongArray, nameUtf8);
            streamWriter.WriteIntRaw(value.Length);
            for (var i = 0; i < value.Length; i++)
                streamWriter.WriteLongRaw(value[i]);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteByteArray(ReadOnlySpan<byte> value)
        {
            streamWriter.OnListItem(NbtTagType.ByteArray);
            streamWriter.WriteIntRaw(value.Length);
            streamWriter.WriteSpan(value);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteIntArray(ReadOnlySpan<int> value)
        {
            streamWriter.OnListItem(NbtTagType.IntArray);
            streamWriter.WriteIntRaw(value.Length);
            for (var i = 0; i < value.Length; i++)
                streamWriter.WriteIntRaw(value[i]);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteLongArray(ReadOnlySpan<long> value)
        {
            streamWriter.OnListItem(NbtTagType.LongArray);
            streamWriter.WriteIntRaw(value.Length);
            for (var i = 0; i < value.Length; i++)
                streamWriter.WriteLongRaw(value[i]);
            return ref streamWriter;
        }
    }
}