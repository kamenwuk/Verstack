using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Writers;

public static class PacketWriterRawExtensions
{
    extension(ref PacketStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteByte(byte value)
        {
            streamWriter.EnsureCapacity(1);
            streamWriter.Buffer[streamWriter.Offset] = value;
            streamWriter.Advance(1);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteSpan(scoped ReadOnlySpan<byte> value)
        {
            streamWriter.EnsureCapacity(value.Length);
            value.CopyTo(streamWriter.Buffer.AsSpan(streamWriter.Offset));
            streamWriter.Advance(value.Length);
            return ref streamWriter;
        }
    }
}