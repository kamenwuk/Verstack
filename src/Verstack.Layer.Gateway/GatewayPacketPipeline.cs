using Verstack.Network.Packet;
using System.Buffers;

namespace Verstack.Layer.Gateway;

internal sealed class GatewayPacketPipeline
{
    private readonly PacketPipeline _pipeline = new(Array.Empty<PacketBundle>());
    
    /// <summary>
    /// Запускает пакет через текущий бандл.
    /// </summary>
    public bool TryProcessPacket(in RawPacket packet, IBufferWriter<byte> writer, ref PacketFlowState state)
    {
        return _pipeline.TryProcessPacket(packet, writer, ref state);
    }
}