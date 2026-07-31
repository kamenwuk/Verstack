using System.Runtime.CompilerServices;
using System.Text;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Запись строковых типов данных (UTF-8 с VarInt-префиксом длины).
/// </summary>
public static class PacketWriterTextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteString(this ref PacketStreamWriter streamWriter, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        streamWriter.WriteVarInt(byteCount); // VarInt сам вызовет EnsureCapacity
        streamWriter.EnsureCapacity(byteCount);
        Encoding.UTF8.GetBytes(value, streamWriter.FreeSpan);
        streamWriter.Advance(byteCount);
        return ref streamWriter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref PacketStreamWriter WriteString(this ref PacketStreamWriter streamWriter, scoped ReadOnlySpan<byte> utf8Value)
    {
        streamWriter.WriteVarInt(utf8Value.Length);
        streamWriter.WriteSpanRaw(utf8Value);
        return ref streamWriter;
    }
}