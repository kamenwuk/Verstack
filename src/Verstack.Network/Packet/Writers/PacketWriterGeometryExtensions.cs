using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Запись геометрических и идентификационных типов данных (UUID, Vectors).
/// </summary>
public static class PacketWriterGeometryExtensions
{
    // ─────────────────────────  UUID  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteUuid(this ref PacketWriter writer, Guid value)
    {
        value.TryWriteBytes(writer.FreeSpan, bigEndian: true, out _);
        writer.Advance(16);
        return ref writer;
    }

    // ─────────────────────────  Vectors  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteVector2(this ref PacketWriter writer, int x, int z)
    {
        writer.WriteInt(x);
        writer.WriteInt(z);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteVector3(this ref PacketWriter writer, int x, int y, int z)
    {
        long value = ((long)x & 0x3FFFFFF) << 38;
        value |= ((long)z & 0x3FFFFFF) << 12;
        value |= (long)y & 0xFFF;
        writer.WriteLong(value);
        return ref writer;
    }
}