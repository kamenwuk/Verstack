using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Запись геометрических и идентификационных типов данных (UUID, Vectors).
/// </summary>
public static class PacketWriterGeometryExtensions
{
    extension(ref PacketStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteUuid(Guid value)
        {
            value.TryWriteBytes(streamWriter.FreeSpan, bigEndian: true, out _);
            streamWriter.Advance(16);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVector2(int x, int z)
        {
            streamWriter.WriteInt(x);
            streamWriter.WriteInt(z);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVector3(int x, int y, int z)
        {
            long value = ((long)x & 0x3FFFFFF) << 38;
            value |= ((long)z & 0x3FFFFFF) << 12;
            value |= (long)y & 0xFFF;
            streamWriter.WriteLong(value);
            return ref streamWriter;
        }
    }
}