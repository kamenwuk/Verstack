using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Layers.Realm.Input.Movement;
using Verstack.Engine.Network.Compression;
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
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;

    private DispatchPacketPipeline _pipeline;
    
    public void Init(IProtoSystems systems)
    {
        _pipeline = new DispatchPacketPipeline(systems, _compressor, new Dictionary<int, PacketBundle>()
        {
            { 0x00, new ConfirmTeleportBundle() },
            { 0x1E, new SetPlayerPositionBundle() },
            { 0x1F, new SetPlayerPositionAndRotationBundle() }
        });
    }
    
    public void Run()
    {
        // Ищем всех сущностей с активной сетевой сессией
        foreach (var entity in _bridgeStateCacheStore.ConnectedFilter)
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