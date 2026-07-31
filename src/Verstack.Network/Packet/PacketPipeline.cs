using Verstack.Network.Compression;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Network.Packet;

public sealed class PacketPipeline
{
    public int BundleCount => _bundles.Length;

    private readonly IPacketCompressor _compressor;
    private readonly PacketBundle[] _bundles;

    public PacketPipeline(IProtoSystems systems, IPacketCompressor compressor, PacketBundle[] bundles)
    {
        _compressor = compressor;
        _bundles = bundles;
        
        for (var idx = 0; idx < _bundles.Length; idx++)
        {
            _bundles[idx].Index = idx;
            _bundles[idx].Init(systems);
        }
    }

    public PipelineSessionStatus ProcessSession(ProtoEntity entity, NetworkChannel channel, ref PacketFlowState state)
    {
        while (channel.IncomingPackets.TryDequeue(out var rawPacket))
        {
            if (state.BundleIndex >= _bundles.Length)
                return PipelineSessionStatus.Transfer;

            var outbound = new PacketOutbound(channel, _compressor);
            try
            {
                PacketHandleResult result;
                do
                {
                    result = TryProcessPacket(entity, rawPacket, ref outbound, ref state);
                    outbound.Flush();
                } while (result == PacketHandleResult.Continue);
                
                if (result == PacketHandleResult.Kick) 
                {
                    Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                    return PipelineSessionStatus.Kick;
                }
            }
            finally
            {
                outbound.Dispose();
            }
        }

        if (state.BundleIndex >= _bundles.Length)
            return PipelineSessionStatus.Transfer;

        return PipelineSessionStatus.Ok;
    }

    private PacketHandleResult TryProcessPacket(ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound, ref PacketFlowState state)
    {
        if (state.BundleIndex < 0 || state.BundleIndex >= _bundles.Length)
            return PacketHandleResult.Kick;

        var bundle = _bundles[state.BundleIndex];
        var result = bundle.TryProcess(state.StepIndex, entity, packet, ref outbound);

        // Если пакет проигнорирован, состояние конвейера не меняется
        if (result == PacketHandleResult.Ignored)
            return result;
        
        // ВАЖНО: Если кик, тоже не двигаем шаги, просто выходим
        if (result == PacketHandleResult.Kick)
            return result;
        
        // Если Accepted или Continue — двигаем шаг!
        state.StepIndex++;
        if (state.StepIndex >= bundle.StepCount)
        {
            state.BundleIndex++;
            state.StepIndex = 0;
        }
        
        return result;
    }
}

public enum PipelineSessionStatus
{
    Ok,
    Kick,
    Transfer
}