using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Verstack.Network.Packet.Writers;

public static class PacketWriterRawExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteByteRaw(this ref PacketStreamWriter streamWriter, byte value)
    {
        streamWriter.EnsureCapacity(1);
        streamWriter.Buffer[streamWriter.Offset] = value;
        streamWriter.Advance(1);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteShortRaw(this ref PacketStreamWriter streamWriter, short value)
    {
        streamWriter.EnsureCapacity(2);
        BinaryPrimitives.WriteInt16BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
        streamWriter.Advance(2);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteUShortRaw(this ref PacketStreamWriter streamWriter, ushort value)
    {
        streamWriter.EnsureCapacity(2);
        BinaryPrimitives.WriteUInt16BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
        streamWriter.Advance(2);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteIntRaw(this ref PacketStreamWriter streamWriter, int value)
    {
        streamWriter.EnsureCapacity(4);
        BinaryPrimitives.WriteInt32BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
        streamWriter.Advance(4);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteLongRaw(this ref PacketStreamWriter streamWriter, long value)
    {
        streamWriter.EnsureCapacity(8);
        BinaryPrimitives.WriteInt64BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
        streamWriter.Advance(8);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteSpanRaw(this ref PacketStreamWriter streamWriter, scoped ReadOnlySpan<byte> value)
    {
        streamWriter.EnsureCapacity(value.Length);
        value.CopyTo(streamWriter.Buffer.AsSpan(streamWriter.Offset));
        streamWriter.Advance(value.Length);
        return ref streamWriter;
    }
}