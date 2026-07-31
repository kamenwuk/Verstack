using System.Runtime.CompilerServices;
using System.Text;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Запись строковых типов данных (UTF-8 с VarInt-префиксом длины).
/// </summary>
public static class PacketWriterTextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteString(this ref PacketWriter writer, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        writer.WriteVarInt(byteCount);
        Encoding.UTF8.GetBytes(value, writer.FreeSpan);
        writer.Advance(byteCount);
        return ref writer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketWriter WriteString(this ref PacketWriter writer, scoped ReadOnlySpan<byte> utf8Value)
    {
        writer.WriteVarInt(utf8Value.Length);
        writer.WriteSpanRaw(utf8Value);
        return ref writer;
    }
}