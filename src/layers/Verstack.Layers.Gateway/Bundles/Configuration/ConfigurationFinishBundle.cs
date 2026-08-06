using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Engine.Lifecycle;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway.Bundles;

internal sealed class ConfigurationFinishBundle : PacketBundle
{
    public override int StepCount => 1;

    private GatewayCacheStore _cache = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.GATEWAY];
        _cache = world.Aspect<GatewayCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id == 0x02)
            return PacketHandleResult.Ignored;
        if (packet.Id != 0x03) 
            return PacketHandleResult.Kick;
        
        string name = _cache.UserProfiles.Get(entity).Username;
        Logger.Debug(LogKey.PacketConfigurationFinish, name);
        
        return PacketHandleResult.Kick;
    }
}