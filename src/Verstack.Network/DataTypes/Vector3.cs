using System.Buffers;

namespace Verstack.Network.DataTypes;

public readonly record struct Vector3(int X, int Y, int Z)
{
    public static Vector3 Read(ref SequenceReader<byte> reader)
    {
        long value = Numeric.ReadLong(ref reader);
        
        // X: 26 бит (биты 38-63)
        int x = (int)(value >> 38);
        // Z: 26 бит (биты 12-37)
        int z = (int)(value << 26 >> 38);
        // Y: 12 бит (биты 0-11)
        int y = (int)(value << 52 >> 52);

        return new Vector3(x, y, z);
    }

    public void Write(IBufferWriter<byte> writer)
    {
        long value = ((long)X & 0x3FFFFFF) << 38;
        value |= ((long)Z & 0x3FFFFFF) << 12;
        value |= (long)Y & 0xFFF;

        Numeric.WriteLong(writer, value);
    }
}