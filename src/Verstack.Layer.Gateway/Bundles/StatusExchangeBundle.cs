using System.Buffers;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Core;
using Verstack.Debug;
using Verstack.Layer.Global;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class StatusExchangeBundle : PacketBundle
{
    public override int StepCount => 1;
    
    private ServerInfoCacheStore _serverInfo = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[WorldScopes.GLOBAL];
        _serverInfo = world.Aspect<ServerInfoCacheStore>();
    }
    
    public override bool TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, IBufferWriter<byte> writer)
    {
        if (packet.Id != 0x00) // Status Request
            return false;
        
        Logger.Debug(LogKey.PacketStatusExchange);

        byte[] json = _serverInfo.GetStatusJson();
        int payload = VarInt.GetSize(0x00) + VarInt.GetSize(json.Length) + json.Length;
        VarInt.Write(writer, payload);
        VarInt.Write(writer, 0x00);                 // Status Response
        VarInt.Write(writer, json.Length);
        json.CopyTo(writer.GetSpan(json.Length));
        writer.Advance(json.Length);
        return true;
    }
}