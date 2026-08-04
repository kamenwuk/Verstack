using Verstack.Engine.Lifecycle;
using Verstack.Engine.Network.Packet;
using Verstack.Engine.Network.Packet.Pipeline;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway.Bundles;

internal sealed class LoginAcknowledgedBundle : PacketBundle
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
        if (packet.Id != 0x03) // Login Acknowledged
            return PacketHandleResult.Kick;

        string name = _cache.UserProfiles.Get(entity).Username;
        Logger.Debug(LogKey.PacketLoginAcknowledged, name);
        return PacketHandleResult.Accepted;
    }
}