using Verstack.Network.Packet.Writers;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Verstack.Layer.Global;
using Verstack.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class StatusExchangeBundle : PacketBundle
{
    public override int StepCount => 1;

    private ServerInfoCacheStore _serverInfo = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.GLOBAL];
        _serverInfo = world.Aspect<ServerInfoCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x00) // Status Request
            return PacketHandleResult.Kick;

        Logger.Debug(LogKey.PacketStatusExchange);

        byte[] json = _serverInfo.GetStatusJson();

        var writer = outbound.Begin();
            
        writer.WriteVarInt(0x00)               // Status Response ID
            .WriteVarInt(json.Length)        // JSON string length
            .WriteSpanRaw(json);             // JSON string payload
            
        outbound.Commit(ref writer);


        return PacketHandleResult.Accepted;
    }
}