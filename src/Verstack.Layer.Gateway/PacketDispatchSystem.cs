using Verstack.Layer.Gateway.Bundles;
using Verstack.Network.Compression;
using Verstack.Layer.Realm.User;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Layer.Gateway;

internal sealed class PacketDispatchSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI] private readonly ZLibPacketCompressor _compressor = null!;
    [DI(ServerWorldScopes.REALM)] private readonly UserSessionCacheStore _userSessionCacheStore = null!;

    private PacketPipeline _pipeline = null!;
    
    public void Init(IProtoSystems systems)
    {
        _pipeline = new PacketPipeline(systems, _compressor, [
            new StatusExchangeBundle(),
            new PingPongBundle(),
            new LoginStartBundle(),
            new LoginAcknowledgedBundle(),
            new ClientInformationBundle(),
            new KnownPacksBundle(),
            new ConfigurationFinishBundle()
        ]);
    }
    
    public void Run()
    {
        foreach (var entity in _gatewayCacheStore.Sessions)
        {
            var channel = _gatewayCacheStore.GetChannel((int)entity);
            if (channel == null)
                continue;

            ref var flowState = ref _gatewayCacheStore.FlowStates.Get(entity);

            var status = _pipeline.ProcessSession(entity, channel, ref flowState);

            if (status == PipelineSessionStatus.Transfer)
            {
                var user = _gatewayCacheStore.UserProfiles.Get(entity);
                var session = _gatewayCacheStore.Sessions.Get(entity);
                
                Logger.Info(LogKey.PacketRealmTransfer, user.Username);
                
                _userSessionCacheStore.Transfer(user, session, channel);
                
                _gatewayCacheStore.World().DelEntity(entity);
                _gatewayCacheStore.RemoveChannel(channel);
                continue;
            }

            if (status == PipelineSessionStatus.Kick)
            {
                channel.Disconnect();
            }
        }
    }
}