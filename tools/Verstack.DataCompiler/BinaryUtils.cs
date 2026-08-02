namespace Verstack.DataCompiler;

public static class BinaryUtils
{
    public static void WriteVarInt(BinaryWriter bw, int value)
    {
        while ((value & -128) != 0)
        {
            bw.Write((byte)(value & 127 | 128));
            value >>>= 7;
        }
        bw.Write((byte)value);
    }
}