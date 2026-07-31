using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Числовые типы данных протокола Minecraft (VarInt, Fixed Integers, Floats, Booleans).
/// </summary>
public static class PacketWriterNumericExtensions
{
    // ─────────────────────────  VarInt / VarLong  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteVarInt(this ref PacketWriter writer, int value)
    {
        uint v = (uint)value;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            writer.WriteByteRaw(temp);
        } while (v != 0);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteVarLong(this ref PacketWriter writer, long value)
    {
        ulong v = (ulong)value;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            writer.WriteByteRaw(temp);
        } while (v != 0);
        return ref writer;
    }

    // ─────────────────────────  Numerics  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteShort(this ref PacketWriter writer, short value) 
        => ref writer.WriteShortRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteUShort(this ref PacketWriter writer, ushort value) 
        => ref writer.WriteUShortRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteInt(this ref PacketWriter writer, int value) 
        => ref writer.WriteIntRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteLong(this ref PacketWriter writer, long value) 
        => ref writer.WriteLongRaw(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteFloat(this ref PacketWriter writer, float value) 
        => ref writer.WriteIntRaw(BitConverter.SingleToInt32Bits(value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteDouble(this ref PacketWriter writer, double value) 
        => ref writer.WriteLongRaw(BitConverter.DoubleToInt64Bits(value));

    // ─────────────────────────  Booleans  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteBool(this ref PacketWriter writer, bool value) 
        => ref writer.WriteByteRaw(value ? (byte)1 : (byte)0);
}