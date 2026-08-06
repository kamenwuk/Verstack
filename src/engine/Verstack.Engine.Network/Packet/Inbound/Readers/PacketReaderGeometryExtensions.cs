using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Inbound;

public static class PacketReaderGeometryExtensions
{
    extension(ref PacketStreamReader streamReader)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid ReadUuid()
        {
            if (streamReader.IsFaulted) return Guid.Empty;

            // 4 + 2 + 2 + 8 = 16 байт
            int a = streamReader.ReadInt();
            short b = streamReader.ReadShort();
            short c = streamReader.ReadShort();
            byte d = streamReader.ReadByteRaw();
            byte e = streamReader.ReadByteRaw();
            byte f = streamReader.ReadByteRaw();
            byte g = streamReader.ReadByteRaw();
            byte h = streamReader.ReadByteRaw();
            byte i = streamReader.ReadByteRaw();
            byte j = streamReader.ReadByteRaw();
            byte k = streamReader.ReadByteRaw();

            if (streamReader.IsFaulted) return Guid.Empty;

            return new Guid(a, b, c, d, e, f, g, h, i, j, k);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int x, int z) ReadVector2()
        {
            int x = streamReader.ReadInt();
            int z = streamReader.ReadInt();
            
            return (x, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int x, int y, int z) ReadVector3()
        {
            long value = streamReader.ReadLong();
            
            if (streamReader.IsFaulted) return (0, 0, 0);

            int x = (int)(value >> 38);
            int z = (int)((value << 26) >> 38);
            int y = (int)((value << 52) >> 52);

            return (x, y, z);
        }
    }
}