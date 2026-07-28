using Verstack.Layer.Global;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Network;
using Verstack.Debug;
using Verstack.Core;

namespace Verstack.Layer.Gateway;

/// <summary>
/// Управляет гостевыми подключениями (до создания ECS-сущности).
/// Очищает мёртвые соединения, обрабатывает Handshake и создаёт ECS-сущность,
/// стартуя с BundleIndex 0 (Status) или 2 (Login). Дальше канал крутит PacketDispatchSystem.
/// </summary>
internal sealed class GuestScreeningSystem : IProtoRunSystem
{
    [DI] private readonly TcpNetworkService _tcpNetworkService = null!;
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI(WorldScopes.GATEWAY)] private readonly ProtoWorld _world = null!;

    private readonly GatewayIntakeHandler _intakeHandler = new();

    private readonly List<NetworkChannel> _awaitingHandshake = [];

    public void Run()
    {
        while (_tcpNetworkService.DisconnectedChannels.TryDequeue(out var deadChannel))
        {
            _awaitingHandshake.Remove(deadChannel);

            // Если канал отвалился, будучи уже в ECS (Status или Login)
            int entityId = _gatewayCacheStore.RemoveChannel(deadChannel);
            if (entityId != -1)
                _world.DelEntity((ProtoEntity)entityId);
        }

        while (_tcpNetworkService.PendingConnections.TryDequeue(out var channel))
        {
            Logger.Debug(LogKey.GatewayNewChannel);
            _awaitingHandshake.Add(channel);
        }

        for (var idx = _awaitingHandshake.Count - 1; idx >= 0; idx--)
        {
            var channel = _awaitingHandshake[idx];
            bool stateChanged = false;
            while (!stateChanged && channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                int nextState = _intakeHandler.TryParseHandshake(rawPacket, out var data);

                switch (nextState)
                {
                    case -1:
                    {
#if DEBUG
                        Logger.Debug(LogKey.GatewayHandshakeRejected, channel.RemoteAddress);
#endif
                        channel.Disconnect();
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true; // Выходим из while
                        break;
                    }
                    case 1: // Status
                    {
                        Logger.Info(LogKey.GatewayStatusState, channel.RemoteAddress);
                        PromoteToSession(channel, in data, bundleIndex: 0);
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true;
                        break;
                    }
                    case 2: // Login
                    {
                        Logger.Info(LogKey.GatewayLoginState, channel.RemoteAddress);
                        PromoteToSession(channel, in data, bundleIndex: 2); // после Status(0) и PingPong(1)
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true;
                        break;
                    }
                    default: throw new Exception();
                }
            }
        }
    }

    /// <summary>
    /// Создаёт ECS-сущность подключения: NetworkSession + PacketFlowState со стартовым BundleIndex.
    /// Дальше канал обрабатывается PacketDispatchSystem через конвейер.
    /// </summary>
    private void PromoteToSession(NetworkChannel channel, in (int protocolVersion, string serverAddress, ushort serverPort) data, int bundleIndex)
    {
        ref var session = ref _gatewayCacheStore.Sessions.NewEntity(out var entity);
        session = new NetworkSession(data.protocolVersion, channel.RemoteAddress,
            data.serverAddress, data.serverPort);

        _gatewayCacheStore.AddChannel((int)entity, channel);

        ref var flowState = ref _gatewayCacheStore.FlowStates.Add(entity);
        flowState.BundleIndex = bundleIndex;
        flowState.StepIndex = 0;
    }
}
