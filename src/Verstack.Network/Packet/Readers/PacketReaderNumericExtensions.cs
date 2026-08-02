using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace Verstack.Network.Packet.Readers
{
    public static class PacketReaderNumericExtensions
    {
        extension(ref PacketStreamReader streamReader)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadVarInt()
            {
                if (streamReader.IsFaulted) return 0;

                int value = 0;
                int shift = 0;
                byte b;

                do
                {
                    if (streamReader.Remaining < 1)
                    {
                        streamReader.SetFaulted();
                        return 0;
                    }

                    b = streamReader.RemainingSpan[0];
                    streamReader.Advance(1);

                    value |= (b & 0x7F) << shift;
                    shift += 7;
                } 
                while ((b & 0x80) != 0 && shift < 35);

                if ((b & 0x80) != 0)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long ReadVarLong()
            {
                if (streamReader.IsFaulted) return 0;

                long value = 0;
                int shift = 0;
                byte b;

                do
                {
                    if (streamReader.Remaining < 1)
                    {
                        streamReader.SetFaulted();
                        return 0;
                    }

                    b = streamReader.RemainingSpan[0];
                    streamReader.Advance(1);

                    value |= (long)(b & 0x7F) << shift;
                    shift += 7;
                } 
                while ((b & 0x80) != 0 && shift < 70);

                if ((b & 0x80) != 0)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public short ReadShort()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 2)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                short value = BinaryPrimitives.ReadInt16BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(2);
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ushort ReadUShort()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 2)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                ushort value = BinaryPrimitives.ReadUInt16BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(2);
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int ReadInt()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 4)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                int value = BinaryPrimitives.ReadInt32BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(4);
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long ReadLong()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 8)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                long value = BinaryPrimitives.ReadInt64BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(8);
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float ReadFloat()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 4)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                int value = BinaryPrimitives.ReadInt32BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(4);
                return BitConverter.Int32BitsToSingle(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double ReadDouble()
            {
                if (streamReader.IsFaulted || streamReader.Remaining < 8)
                {
                    streamReader.SetFaulted();
                    return 0;
                }

                long value = BinaryPrimitives.ReadInt64BigEndian(streamReader.RemainingSpan);
                streamReader.Advance(8);
                return BitConverter.Int64BitsToDouble(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ReadBool() 
                => streamReader.ReadByteRaw() != 0;
        }
    }
}