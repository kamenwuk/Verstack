using Verstack.Layers.Global.User;
using Verstack.Engine.Network;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway;

internal sealed class GuestScreeningSystem : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;

    public void Run()
    {
        // Достаем новых игроков напрямую из Bridge. 
        // В payload.Entity уже вшит BridgeClientConnected (он на рельсах).
        // payload.Data для Gateway будет null, так как это первый слой.
        while (_bridgeStateCacheStore.TryDequeueHandoff(out var payload))
        {
            var entity = payload.Entity;
            var channel = _bridgeStateCacheStore.GetChannel(entity);
            if (channel == null) continue;

            bool stateChanged = false;
            
            // Читаем пакеты, пока состояние не изменилось
            while (!stateChanged && channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                int nextState = GatewayIntakeHandler.TryParseHandshake(rawPacket, out var data);

                switch (nextState)
                {
                    case -1: // Отклонено
                        Logger.Warn(LogKey.GatewayHandshakeRejected, channel.RemoteAddress);
                        channel.Disconnect(); 
                        stateChanged = true; // Выходим из while, чтобы не читать пакеты от мертвого канала
                        break;
                        
                    case 1: // Status (Ping)
                        Logger.Info(LogKey.GatewayStatusState, channel.RemoteAddress);
                        PromoteToSession(entity, channel, in data, bundleIndex: 0);
                        stateChanged = true; // Выходим из while, остальные пакеты обработает PacketDispatchSystem
                        break;
                        
                    case 2: // Login
                        Logger.Info(LogKey.GatewayLoginState, channel.RemoteAddress);
                        PromoteToSession(entity, channel, in data, bundleIndex: 2);
                        stateChanged = true; // Выходим из while
                        break;
                }
            }
        }
    }

    private void PromoteToSession(ProtoEntity entity, NetworkChannel channel, in (int protocolVersion, string serverAddress, ushort serverPort) data, int bundleIndex)
    {
        ref var session = ref _gatewayCacheStore.Sessions.Add(entity);
        session = new NetworkSession(data.protocolVersion, channel.RemoteAddress,
            data.serverAddress, data.serverPort);

        ref var flowState = ref _gatewayCacheStore.FlowStates.Add(entity);
        flowState.BundleIndex = bundleIndex;
        flowState.StepIndex = 0;
    }
}