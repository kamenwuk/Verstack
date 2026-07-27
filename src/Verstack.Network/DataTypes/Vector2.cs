using System.Buffers;

namespace Verstack.Network.DataTypes;

public readonly record struct Vector2(int X, int Z)
{
    public static Vector2 Read(ref SequenceReader<byte> reader)
    {
        int x = Numeric.ReadInt(ref reader);
        int z = Numeric.ReadInt(ref reader);
        
        return new Vector2(x, z);
    }

    public void Write(IBufferWriter<byte> writer)
    {
        Numeric.WriteInt(writer, X);
        Numeric.WriteInt(writer, Z);
    }
}