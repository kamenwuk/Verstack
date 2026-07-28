using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Core;
using Verstack.Debug;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class LoginAcknowledgedBundle : PacketBundle
{
    public override int StepCount => 1;

    private GatewayCacheStore _cache = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[WorldScopes.GATEWAY];
        _cache = world.Aspect<GatewayCacheStore>();
    }

    public override bool TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x03) // Login Acknowledged
            return false;

        string name = _cache.UserProfiles.Get(entity).Username;
        Logger.Debug(LogKey.PacketLoginAcknowledged, name);
        return true;
    }
}