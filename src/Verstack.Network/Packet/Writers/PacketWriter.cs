using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// GC-free писатель для формирования payload пакетов Minecraft.
/// Stateful ref struct: помнит смещение (_offset) и пишет прямо в Span&lt;byte&gt;.
/// Fluent API реализован в PacketWriterExtensions, т.к. ref struct не может возвращать ref this.
/// </summary>
public ref struct PacketWriter(Span<byte> buffer)
{
    public int Written => Offset;
    public ReadOnlySpan<byte> WrittenSpan => Buffer[..Offset];
    
    internal Span<byte> FreeSpan => Buffer[Offset..];
    internal readonly Span<byte> Buffer = buffer;
    internal int Offset = 0;
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(int count) => Offset += count;
}