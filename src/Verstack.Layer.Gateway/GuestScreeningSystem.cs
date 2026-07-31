using Verstack.Layer.Global.User;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Network;
using Verstack.Debug;

namespace Verstack.Layer.Gateway;

internal sealed class GuestScreeningSystem : IProtoRunSystem
{
    [DI] private readonly TcpNetworkService _tcpNetworkService = null!;
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI(ServerWorldScopes.GATEWAY)] private readonly ProtoWorld _world = null!;

    private readonly List<NetworkChannel> _awaitingHandshake = [];

    public void Run()
    {
        while (_tcpNetworkService.DisconnectedChannels.TryDequeue(out var deadChannel))
        {
            Logger.Info(LogKey.NetworkChannelDisconnected, deadChannel.RemoteAddress);
            _awaitingHandshake.Remove(deadChannel);

            int entityId = _gatewayCacheStore.RemoveChannel(deadChannel);
            if (entityId != -1)
                _world.DelEntity((ProtoEntity)entityId);
        }

        while (_tcpNetworkService.PendingConnections.TryDequeue(out var channel))
        {
            Logger.Info(LogKey.GatewayNewChannel, channel.RemoteAddress);
            _awaitingHandshake.Add(channel);
        }

        for (var idx = _awaitingHandshake.Count - 1; idx >= 0; idx--)
        {
            var channel = _awaitingHandshake[idx];
            bool stateChanged = false;
            while (!stateChanged && channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                int nextState = GatewayIntakeHandler.TryParseHandshake(rawPacket, out var data);

                switch (nextState)
                {
                    case -1:
                    {
                        Logger.Warn(LogKey.GatewayHandshakeRejected, channel.RemoteAddress);
                        channel.Disconnect();
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true;
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
                        PromoteToSession(channel, in data, bundleIndex: 2);
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true;
                        break;
                    }
                }
            }
        }
    }

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