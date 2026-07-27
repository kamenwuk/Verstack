using Leopotam.EcsProto;

namespace Verstack.Network.Packet;

public abstract class PacketProcessor
{
    protected internal abstract bool TryProcess(ProtoEntity entity, NetworkChannel channel, in RawPacket packet);
}