using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Layers.Realm.Input.Movement;
using Verstack.Engine.Network.Compression;
using Verstack.Layers.Realm.Shared;
using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input;

/// <summary>
/// ECS-система, отвечающая за маршрутизацию входящих пакетов стадии Play.
/// Запускается в самом начале тика, вычитывает все пакеты игроков и передает их в диспетчер.
/// </summary>
internal sealed class InboundDispatcherSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    private DispatchPacketPipeline _pipeline = null!;
    
    public void Init(IProtoSystems systems)
    {
        var moveInput = new AcceptMoveInputBundle();
        
        _pipeline = new DispatchPacketPipeline(systems, _compressor, new Dictionary<int, PacketBundle>()
        {
            { 0x00, new ConfirmTeleportBundle() },
            { 0x1E, moveInput },
            { 0x1F, moveInput }
        });
    }
    
    public void Run()
    {
        foreach (var entity in _userSessionCacheStore.ItPlaying)
        {
            var channel = _bridgeStateCacheStore.GetChannel(entity);
            var status = _pipeline.ProcessSession(entity, channel);
            if (status != PipelineSessionStatus.Kick)
                continue;

            channel.Disconnect();
            Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
        }
    }
}