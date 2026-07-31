using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Leopotam.EcsProto;
using System.Buffers;
using Verstack.Debug;
using Verstack.Network.Packet.Writers;

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

        var writer = new PacketWriter(outbound.PayloadBuffer);
        writer.WriteVarInt(0x01)
            .WriteLong(timestamp);
        
        outbound.Send(writer.WrittenSpan);
        return PacketHandleResult.Accepted;
    }
}