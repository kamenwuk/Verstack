using System.Buffers;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class PingPongBundle : PacketBundle
{
    public override int StepCount => 1;
    
    public override bool TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, IBufferWriter<byte> writer)
    {
        if (packet.Id != 0x01) // Ping Request
            return false;

        Logger.Debug(LogKey.PacketPingPong);

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        long timestamp = Numeric.ReadLong(ref reader);

        VarInt.Write(writer, VarInt.GetSize(0x01) + sizeof(long));
        VarInt.Write(writer, 0x01);                 // Pong Response
        Numeric.WriteLong(writer, timestamp);
        return true;
    }
}