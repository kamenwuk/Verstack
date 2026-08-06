using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Engine.Lifecycle;
using Verstack.Layers.Global;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway.Bundles;

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
            .WriteSpan(json);             // JSON string payload
            
        outbound.Commit(ref writer);


        return PacketHandleResult.Accepted;
    }
}