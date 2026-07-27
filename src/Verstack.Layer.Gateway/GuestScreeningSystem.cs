using Verstack.Layer.Global;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Network;
using System.Buffers;
using Verstack.Debug;
using Verstack.Core;

namespace Verstack.Layer.Gateway;

/// <summary>
/// Управляет гостевыми подключениями (до создания ECS-сущности).
/// Очищает мертвые соединения, обрабатывает Handshake и Status (MOTD/Ping).
/// </summary>
internal sealed class GuestScreeningSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly TcpNetworkService _tcpNetworkService = null!;
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI(WorldScopes.GATEWAY)] private readonly ProtoWorld _world = null!;
    
    private GatewayIntakeHandler _intakeHandler;

    private readonly List<NetworkChannel> _awaitingHandshake = [];
    private readonly List<NetworkChannel> _statusConnections = [];
    
    public void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[WorldScopes.GLOBAL];
        _intakeHandler = new GatewayIntakeHandler(world.Aspect<ServerInfoCacheStore>());
    }

    public void Run()
    {
        while (_tcpNetworkService.DisconnectedChannels.TryDequeue(out var deadChannel))
        {
            _awaitingHandshake.Remove(deadChannel);
            _statusConnections.Remove(deadChannel);

            // Если канал отвалился, будучи уже в ECS (например, во время Login)
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
                    case 1:
                    {
                        Logger.Info(LogKey.GatewayStatusState, channel.RemoteAddress);
                        _awaitingHandshake.RemoveAt(idx);
                        _statusConnections.Add(channel);
                        stateChanged = true; // Выходим из while
                        break;
                    }
                    case 2:
                    {
                        Logger.Info(LogKey.GatewayLoginState, channel.RemoteAddress);
                        ref var session = ref _gatewayCacheStore.Sessions.NewEntity(out var entity);
                        session = new NetworkSession(data.protocolVersion, channel.RemoteAddress,
                            data.serverAddress, data.serverPort);
                        
                        _gatewayCacheStore.AddChannel((int)entity, channel);

                        ref var flowState = ref _gatewayCacheStore.FlowStates.Add(entity);
                        flowState.BundleIndex = 0; // Старт конвейера с LoginBundle
                
                        _awaitingHandshake.RemoveAt(idx);
                        stateChanged = true; // Выходим из while
                        break;
                    }
                    default: throw new Exception();
                }
            }
        }
        
        for (var idx = _statusConnections.Count - 1; idx >= 0; idx--)
        {
            var channel = _statusConnections[idx];
            // Handler пишет в IBufferWriter<byte> — передаём временный буфер, не PipeWriter.
            // PipeWriter принадлежит send-воркеру (single-writer контракт Pipes).
            var tempWriter = new ArrayBufferWriter<byte>();

            while (channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                if (!_intakeHandler.TryHandleStatusRequest(rawPacket, tempWriter))
                {
                    Logger.Warn(LogKey.GatewayStatusInvalidPacket, channel.RemoteAddress);
                    channel.Disconnect();
                    _statusConnections.RemoveAt(idx);
                    break;
                }
            }

            // Ставим всё накопленное в outbound-очередь, send-воркер отправит асинхронно.
            if (tempWriter.WrittenCount > 0)
                channel.EnqueueOutbound(tempWriter.WrittenSpan);
        }
    }
}