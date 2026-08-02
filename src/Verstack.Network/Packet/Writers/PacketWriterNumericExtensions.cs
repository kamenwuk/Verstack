using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Числовые типы данных протокола Minecraft (VarInt, Fixed Integers, Floats, Booleans).
/// </summary>
public static class PacketWriterNumericExtensions
{
    extension(ref PacketStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVarInt(int value)
        {
            uint v = (uint)value;
            do
            {
                byte temp = (byte)(v & 0x7F);
                v >>= 7;
                if (v != 0) temp |= 0x80;
                streamWriter.WriteByte(temp);
            } while (v != 0);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVarLong(long value)
        {
            ulong v = (ulong)value;
            do
            {
                byte temp = (byte)(v & 0x7F);
                v >>= 7;
                if (v != 0) temp |= 0x80;
                streamWriter.WriteByte(temp);
            } while (v != 0);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteShort(short value)
        {
            streamWriter.EnsureCapacity(2);
            BinaryPrimitives.WriteInt16BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
            streamWriter.Advance(2);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteUShort(ushort value)
        {
            streamWriter.EnsureCapacity(2);
            BinaryPrimitives.WriteUInt16BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
            streamWriter.Advance(2);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteInt(int value)
        {
            streamWriter.EnsureCapacity(4);
            BinaryPrimitives.WriteInt32BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
            streamWriter.Advance(4);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteLong(long value)
        {
            streamWriter.EnsureCapacity(8);
            BinaryPrimitives.WriteInt64BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), value);
            streamWriter.Advance(8);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteFloat(float value)
        {
            streamWriter.EnsureCapacity(4);
            BinaryPrimitives.WriteInt32BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), BitConverter.SingleToInt32Bits(value));
            streamWriter.Advance(4);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteDouble(double value)
        {
            streamWriter.EnsureCapacity(8);
            BinaryPrimitives.WriteInt64BigEndian(streamWriter.Buffer.AsSpan(streamWriter.Offset), BitConverter.DoubleToInt64Bits(value));
            streamWriter.Advance(8);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteBool(bool value) 
            => ref streamWriter.WriteByte(value ? (byte)1 : (byte)0);
    }
}