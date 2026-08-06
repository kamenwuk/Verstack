using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel.Encoding;

/// <summary>
/// Запись LEB128 VarInt в Span (wire-формат протокола Minecraft).
/// Дублирует PacketStreamWriter.WriteVarInt намеренно: Shared.Voxel не зависит
/// от Engine.Network, а VarInt — часть wire-формата Paletted Container.
/// </summary>
internal static class VarIntEncoding
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write(Span<byte> dest, int value)
    {
        uint v = (uint)value;
        int written = 0;
        do
        {
            byte temp = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) temp |= 0x80;
            dest[written++] = temp;
        } while (v != 0);
        return written;
    }
}