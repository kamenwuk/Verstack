using Verstack.Network.Packet.Writers;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Leopotam.EcsProto;
using System.Buffers;
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

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        long timestamp = Numeric.ReadLong(ref reader);

        var writer = outbound.Begin();
        {
            writer.WriteVarInt(0x01)
                .WriteLong(timestamp);
        }
        outbound.Commit(ref writer);
        
        return PacketHandleResult.Accepted;
    }
}