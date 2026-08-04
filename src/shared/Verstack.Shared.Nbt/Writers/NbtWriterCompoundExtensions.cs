using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>Именованные скаляры для Compound-контекста: тег с именем и type-байтом.</summary>
public static class NbtWriterCompoundExtensions
{
    extension(ref NbtStreamWriter writer)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteByte(ReadOnlySpan<byte> nameUtf8, sbyte value)
        {
            writer.WriteNameAndType(NbtTagType.Byte, nameUtf8);
            writer.WriteByteRaw((byte)value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteShort(ReadOnlySpan<byte> nameUtf8, short value)
        {
            writer.WriteNameAndType(NbtTagType.Short, nameUtf8);
            writer.WriteShortRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteInt(ReadOnlySpan<byte> nameUtf8, int value)
        {
            writer.WriteNameAndType(NbtTagType.Int, nameUtf8);
            writer.WriteIntRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteLong(ReadOnlySpan<byte> nameUtf8, long value)
        {
            writer.WriteNameAndType(NbtTagType.Long, nameUtf8);
            writer.WriteLongRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteFloat(ReadOnlySpan<byte> nameUtf8, float value)
        {
            writer.WriteNameAndType(NbtTagType.Float, nameUtf8);
            writer.WriteIntRaw(BitConverter.SingleToInt32Bits(value));
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteDouble(ReadOnlySpan<byte> nameUtf8, double value)
        {
            writer.WriteNameAndType(NbtTagType.Double, nameUtf8);
            writer.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteString(ReadOnlySpan<byte> nameUtf8, ReadOnlySpan<byte> valueUtf8)
        {
            writer.WriteNameAndType(NbtTagType.String, nameUtf8);
            writer.WriteStringPayload(valueUtf8);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteBool(ReadOnlySpan<byte> nameUtf8, bool value)
        {
            writer.WriteNameAndType(NbtTagType.Byte, nameUtf8);
            writer.WriteByteRaw(value ? (byte)1 : (byte)0);
            return ref writer;
        }
    }
}