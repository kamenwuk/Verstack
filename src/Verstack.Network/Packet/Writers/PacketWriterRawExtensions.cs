using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Verstack.Network.Packet.Writers;

public static class PacketWriterRawExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteByteRaw(this ref PacketStreamWriter streamWriter, byte value)
    {
        streamWriter.Buffer[streamWriter.Offset] = value;
        streamWriter.Advance(1); // Было: writer.Offset += 1;
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteShortRaw(this ref PacketStreamWriter streamWriter, short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(streamWriter.Buffer[streamWriter.Offset..], value);
        streamWriter.Advance(2); // Было: writer.Offset += 2;
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteUShortRaw(this ref PacketStreamWriter streamWriter, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(streamWriter.Buffer[streamWriter.Offset..], value);
        streamWriter.Advance(2);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteIntRaw(this ref PacketStreamWriter streamWriter, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(streamWriter.Buffer[streamWriter.Offset..], value);
        streamWriter.Advance(4);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteLongRaw(this ref PacketStreamWriter streamWriter, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(streamWriter.Buffer[streamWriter.Offset..], value);
        streamWriter.Advance(8);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteSpanRaw(this ref PacketStreamWriter streamWriter, scoped ReadOnlySpan<byte> value)
    {
        value.CopyTo(streamWriter.Buffer[streamWriter.Offset..]);
        streamWriter.Advance(value.Length);
        return ref streamWriter;
    }
}