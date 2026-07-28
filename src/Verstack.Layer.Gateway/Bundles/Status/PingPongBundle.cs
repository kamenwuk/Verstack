using System.Buffers;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class PingPongBundle : PacketBundle
{
    public override int StepCount => 1;

    public override bool TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x01) // Ping Request
            return false;

        Logger.Debug(LogKey.PacketPingPong);

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        long timestamp = Numeric.ReadLong(ref reader);

        var pw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref pw, 0x01);                       // Pong Response ID
        Numeric.WriteLong(ref pw, timestamp);
        outbound.Send(pw.WrittenSpan);
        return true;
    }
}