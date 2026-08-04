using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>Безымянные скаляры для List-контекста: элемент без имени и type-байта (тип/количество уже в заголовке List).</summary>
public static class NbtWriterListExtensions
{
    extension(ref NbtStreamWriter writer)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemByte(sbyte value)
        {
            writer.OnListItem(NbtTagType.Byte);
            writer.WriteByteRaw((byte)value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemShort(short value)
        {
            writer.OnListItem(NbtTagType.Short);
            writer.WriteShortRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemInt(int value)
        {
            writer.OnListItem(NbtTagType.Int);
            writer.WriteIntRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemLong(long value)
        {
            writer.OnListItem(NbtTagType.Long);
            writer.WriteLongRaw(value);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemFloat(float value)
        {
            writer.OnListItem(NbtTagType.Float);
            writer.WriteIntRaw(BitConverter.SingleToInt32Bits(value));
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemDouble(double value)
        {
            writer.OnListItem(NbtTagType.Double);
            writer.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemString(ReadOnlySpan<byte> valueUtf8)
        {
            writer.OnListItem(NbtTagType.String);
            writer.WriteStringPayload(valueUtf8);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter WriteListItemBool(bool value)
        {
            writer.OnListItem(NbtTagType.Byte);
            writer.WriteByteRaw(value ? (byte)1 : (byte)0);
            return ref writer;
        }
    }
}