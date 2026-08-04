using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Потребление payload после peek имени в Compound: за <see cref="NbtReaderExtensions.ReadTagName"/> следует
/// конкретный ReadXxxPayload по типу тега. Type-байт и имя уже прочитаны — methods читают только payload.
/// </summary>
public static class NbtReaderPayloadExtensions
{
    extension(ref NbtStreamReader reader)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadBytePayload() => (sbyte)reader.ReadByteRaw();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShortPayload() => reader.ReadShortRaw();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadIntPayload() => reader.ReadIntRaw();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLongPayload() => reader.ReadLongRaw();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloatPayload() => BitConverter.Int32BitsToSingle(reader.ReadIntRaw());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDoublePayload() => BitConverter.Int64BitsToDouble(reader.ReadLongRaw());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolPayload() => reader.ReadByteRaw() != 0;
    }
}