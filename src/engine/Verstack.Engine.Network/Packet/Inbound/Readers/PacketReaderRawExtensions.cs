using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Inbound;

public static class PacketReaderRawExtensions
{
    extension(ref PacketStreamReader streamReader)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByteRaw()
        {
            if (streamReader.IsFaulted || streamReader.Remaining < 1)
            {
                streamReader.SetFaulted();
                return 0;
            }

            byte value = streamReader.RemainingSpan[0];
            streamReader.Advance(1);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadSpanRaw(int count)
        {
            if (streamReader.IsFaulted || streamReader.Remaining < count || count < 0)
            {
                streamReader.SetFaulted();
                return ReadOnlySpan<byte>.Empty;
            }

            var span = streamReader.RemainingSpan.Slice(0, count);
            streamReader.Advance(count);
            return span;
        }
    }
}