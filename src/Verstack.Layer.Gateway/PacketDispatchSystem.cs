using Verstack.Layer.Gateway.Bundles;
using Verstack.Network.Compression;
using Verstack.Network.Packet;
using Verstack.Shared.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Lifecycle;

namespace Verstack.Layer.Gateway;

internal sealed class PacketDispatchSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

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
        foreach (var entity in _bridgeStateCacheStore.ConnectedFilter)
        {
            if(!_gatewayCacheStore.ActiveSessionsFilter.Has(entity))
                continue;
            
            var channel = _bridgeStateCacheStore.GetChannel(entity);

            ref var flowState = ref _gatewayCacheStore.FlowStates.Get(entity);

            var status = _pipeline.ProcessSession(entity, channel, ref flowState);

            if (status == PipelineSessionStatus.Transfer)
            {
                // Пайплайн завершил конфигурацию. 
                // Мы НИЧЕГО не делаем. GatewayNetworkHandoffPolicy в NetworkCleanupSystem
                // увидит, что FlowState дошел до конца, и сама перенесет игрока в Realm.
                continue;
            }

            if (status == PipelineSessionStatus.Kick)
            {
                // Нарушение протокола. Рвем соединение. 
                // Сеть сообщит роутеру, вешается NetworkDisconnectedState, сущность удалится.
                channel.Disconnect();
            }
        }
    }
}