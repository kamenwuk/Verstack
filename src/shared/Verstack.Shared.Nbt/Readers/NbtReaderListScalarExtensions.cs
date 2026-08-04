using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Безымянные скаляры для List-контекста: элемент без имени и type-байта (тип/количество уже в заголовке List).
/// </summary>
public static class NbtReaderListScalarExtensions
{
    extension(ref NbtStreamReader reader)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadByte()
        {
            reader.OnListScalar(NbtTagType.Byte);
            return (sbyte)reader.ReadByteRaw();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShort()
        {
            reader.OnListScalar(NbtTagType.Short);
            return reader.ReadShortRaw();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt()
        {
            reader.OnListScalar(NbtTagType.Int);
            return reader.ReadIntRaw();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLong()
        {
            reader.OnListScalar(NbtTagType.Long);
            return reader.ReadLongRaw();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat()
        {
            reader.OnListScalar(NbtTagType.Float);
            return BitConverter.Int32BitsToSingle(reader.ReadIntRaw());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            reader.OnListScalar(NbtTagType.Double);
            return BitConverter.Int64BitsToDouble(reader.ReadLongRaw());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadString(Span<char> destination, out int charsWritten)
        {
            reader.OnListScalar(NbtTagType.String);
            reader.ReadStringPayload(destination, out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool()
        {
            reader.OnListScalar(NbtTagType.Byte);
            return reader.ReadByteRaw() != 0;
        }
    }
}