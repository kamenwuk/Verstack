using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>Raw-запись BE-скаляров и массивов байт — фундамент остальных extensions.</summary>
internal static class NbtWriterRawExtensions
{
    extension(ref NbtStreamWriter writer)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteTagType(NbtTagType type) => writer.WriteByteRaw((byte)type);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteByteRaw(byte value)
        {
            writer.Buffer[writer.Offset] = value;
            writer.Offset += 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteShortRaw(short value)
        {
            BinaryPrimitives.WriteInt16BigEndian(writer.Buffer[writer.Offset..], value);
            writer.Offset += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteIntRaw(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(writer.Buffer[writer.Offset..], value);
            writer.Offset += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteLongRaw(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(writer.Buffer[writer.Offset..], value);
            writer.Offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteSpan(ReadOnlySpan<byte> value)
        {
            value.CopyTo(writer.Buffer[writer.Offset..]);
            writer.Offset += value.Length;
        }
    }
}