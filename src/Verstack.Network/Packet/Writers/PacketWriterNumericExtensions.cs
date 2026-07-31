using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Числовые типы данных протокола Minecraft (VarInt, Fixed Integers, Floats, Booleans).
/// </summary>
public static class PacketWriterNumericExtensions
{
    // ─────────────────────────  VarInt / VarLong  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteVarInt(this ref PacketStreamWriter streamWriter, int value)
    {
        uint v = (uint)value;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            streamWriter.WriteByteRaw(temp);
        } while (v != 0);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteVarLong(this ref PacketStreamWriter streamWriter, long value)
    {
        ulong v = (ulong)value;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            streamWriter.WriteByteRaw(temp);
        } while (v != 0);
        return ref streamWriter;
    }

    // ─────────────────────────  Numerics  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteShort(this ref PacketStreamWriter streamWriter, short value) 
        => ref streamWriter.WriteShortRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteUShort(this ref PacketStreamWriter streamWriter, ushort value) 
        => ref streamWriter.WriteUShortRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteInt(this ref PacketStreamWriter streamWriter, int value) 
        => ref streamWriter.WriteIntRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteLong(this ref PacketStreamWriter streamWriter, long value) 
        => ref streamWriter.WriteLongRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteFloat(this ref PacketStreamWriter streamWriter, float value) 
        => ref streamWriter.WriteIntRaw(BitConverter.SingleToInt32Bits(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteDouble(this ref PacketStreamWriter streamWriter, double value) 
        => ref streamWriter.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));

    // ─────────────────────────  Booleans  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteBool(this ref PacketStreamWriter streamWriter, bool value) 
        => ref streamWriter.WriteByteRaw(value ? (byte)1 : (byte)0);
}