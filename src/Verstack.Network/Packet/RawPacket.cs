namespace Verstack.Network.Packet;

public readonly struct RawPacket(int id, byte[] data)
{
    public readonly int Id = id;
    public readonly byte[] Data = data;
}