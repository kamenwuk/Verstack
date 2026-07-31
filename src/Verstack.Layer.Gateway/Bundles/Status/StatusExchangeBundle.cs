using Verstack.Network.DataTypes;
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

        var pw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref pw, 0x00);                       // Status Response ID
        VarInt.Write(ref pw, json.Length);
        json.CopyTo(pw.GetSpan(json.Length));
        pw.Advance(json.Length);
        outbound.Send(pw.WrittenSpan);
        return PacketHandleResult.Accepted;
    }
}