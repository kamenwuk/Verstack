using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Низкоуровневые методы записи сырых байт и BE-скаляров в PacketWriter.
/// </summary>
public static class PacketWriterRawExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteByteRaw(this ref PacketWriter writer, byte value)
    {
        writer.Buffer[writer.Offset] = value;
        writer.Offset += 1;
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteShortRaw(this ref PacketWriter writer, short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(writer.Buffer[writer.Offset..], value);
        writer.Offset += 2;
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteUShortRaw(this ref PacketWriter writer, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(writer.Buffer[writer.Offset..], value);
        writer.Offset += 2;
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteIntRaw(this ref PacketWriter writer, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(writer.Buffer[writer.Offset..], value);
        writer.Offset += 4;
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteLongRaw(this ref PacketWriter writer, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(writer.Buffer[writer.Offset..], value);
        writer.Offset += 8;
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteSpanRaw(this ref PacketWriter writer, scoped ReadOnlySpan<byte> value)
    {
        value.CopyTo(writer.Buffer[writer.Offset..]);
        writer.Offset += value.Length;
        return ref writer;
    }
}