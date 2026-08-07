using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Compression;
using Verstack.Layers.Realm.Session;
using Verstack.Layers.Realm.Shared;
using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Verstack.Layers.Global;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// Приём перехода игрока из Gateway: одобряет handoff, сеет ECS-компоненты
/// (UserProfiles/Sessions/FlowStates) для нормального входа в Play, после чего
/// гоняет join-pipeline (6 бандлов) по подключённым сущностям.
/// </summary>
internal sealed class HandoffApprovalSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    private SequentialPacketPipeline _pipeline = null!;
    private HandoffSeeder _handoffSeeder = null!;
    
    public void Init(IProtoSystems systems)
    {
        _handoffSeeder = new HandoffSeeder();
        _handoffSeeder.Init(systems);
        
        _pipeline = new SequentialPacketPipeline(systems, _compressor, [
            new JoinLoginBundle(),
            new JoinSpawnPointBundle(),
            new JoinTabListBundle(),
            new JoinCommandCatalogBundle(),
            new JoinChunkBatchBundle(),
            new JoinTeleportBundle(),
        ]);
    }
    
    public void Run()
    {
        // Фаза 1 — одобрение handoff: вычитываем переходы из Gateway и сеем ECS-компоненты,
        // чтобы сущность была готова к нормальному входу в стадию Play.
        while (_bridgeStateCacheStore.TryDequeueHandoff(out var payload))
        {
            if (payload.Data is EnterRealmHandoffData realmData)
            {
                var entity = payload.Entity;
                
                _userSessionCacheStore.UserProfiles.Add(entity) = realmData.Profile;
                _userSessionCacheStore.Sessions.Add(entity) = realmData.Session;
                _userSessionCacheStore.FlowStates.Add(entity) = new PacketFlowState(0, 0);
            }
        }
        
        // Фаза 2 — join-pipeline: прогоняем join-бандлы по подключённым сущностям.
        foreach (var entity in _userSessionCacheStore.ItJoining)
        {
            var channel = _bridgeStateCacheStore.GetChannel(entity);
            
            ref var flowState = ref _userSessionCacheStore.FlowStates.Get(entity);

            var status = _pipeline.ProcessSession(entity, channel, ref flowState);

            switch (status)
            {
                case PipelineSessionStatus.Transfer:
                {
                    _userSessionCacheStore.FlowStates.Del(entity);
                    _handoffSeeder.Seed(entity);
                    break;
                }
                case PipelineSessionStatus.Kick:
                {
                    // Нарушение протокола — рвём соединение. Сеть сообщит роутеру,
                    // BridgeDisconnectSystem повесит BridgeClientDisconnected,
                    // BridgeCleanupSystem удалит сущность.
                    channel.Disconnect();
                    break;
                }
            }
        }
        

    }
}