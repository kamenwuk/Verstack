using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

public ref struct PacketStreamWriter
{
    public int Written => Offset;
    public ReadOnlySpan<byte> WrittenSpan => Buffer[..Offset];
    
    internal Span<byte> FreeSpan => Buffer[Offset..];
    internal readonly Span<byte> Buffer;
    internal int Offset = 0;
    
    internal PacketStreamWriter(Span<byte> buffer)
    {
        Buffer = buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(int count) => Offset += count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Offset = 0;
}