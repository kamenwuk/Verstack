using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Network.Packet.Writers;

public ref struct PacketStreamWriter
{
    public int Written => Offset;
    public ReadOnlySpan<byte> WrittenSpan => Buffer.AsSpan(0, Offset);
    
    internal Span<byte> FreeSpan => Buffer.AsSpan(Offset);
    internal byte[] Buffer;
    internal int Offset = 0;
    
    internal PacketStreamWriter(byte[] buffer, int offset = 0)
    {
        Buffer = buffer;
        Offset = offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureCapacity(int count)
    {
        if (Offset + count > Buffer.Length)
        {
            int newSize = Buffer.Length;
            while (newSize < Offset + count)
                newSize *= 2;

            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.AsSpan(0, Offset).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(Buffer);
            Buffer = newBuffer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(int count) => Offset += count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Offset = 0;
}