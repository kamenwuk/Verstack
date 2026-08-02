using Verstack.Network.Packet.Writers;
using Verstack.Network.Packet.Readers;
using Verstack.Network.Packet;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class PingPongBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x01) // Ping Request
            return PacketHandleResult.Kick;

        Logger.Debug(LogKey.PacketPingPong);

        //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        var reader = packet.CreateReader();
        
        long timestamp = reader.ReadLong();

        if (reader.IsFaulted)
            return PacketHandleResult.Kick;
        
        var writer = outbound.Begin();
        {
            writer.WriteVarInt(0x01)
                .WriteLong(timestamp);
        }
        outbound.Commit(ref writer);
        
        return PacketHandleResult.Accepted;
    }
}