using Verstack.Layer.Gateway.Bundles;
using Verstack.Network.Compression;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using System.Buffers;
using Verstack.Debug;
using Verstack.Layer.Realm.User;
using Verstack.Lifecycle;

namespace Verstack.Layer.Gateway;

internal sealed class PacketDispatchSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI] private readonly ZLibPacketCompressor _compressor = null!;
    [DI(ServerWorldScopes.REALM)] private readonly UserSessionCacheStore _userSessionCacheStore = null!;

    // Буферы арендуются один раз на весь Run, возвращаются в finally (heap → GC-free на cold path).
    //   frameScratch   — framing-выход, contiguous, растёт вправо от 0. Отправляется в канал.
    //   payloadBuffer  — временный буфер под payload текущего пакета; бандл пишет в него через SpanWriter.
    // Увеличено для поддержки больших пакетов Registry Data (до 16 КБ).
    private const int FRAME_SCRATCH_SIZE = 16 * 1024;
    private const int PAYLOAD_BUFFER_SIZE = 16 * 1024;   // было 4*1024, теперь 16 КБ

    private PacketPipeline _pipeline = null!;
    
    public void Init(IProtoSystems systems)
    {
        _pipeline = new PacketPipeline(systems, [
            new StatusExchangeBundle(),
            new PingPongBundle(),
            new LoginStartBundle(),          // ← индекс 2
            new LoginAcknowledgedBundle(),   // ← индекс 3
            new ClientInformationBundle(),   // ← индекс 4 (Configuration)
            new KnownPacksBundle(),          // ← индекс 5
            new ConfigurationFinishBundle()  // ← индекс 6
        ]);
    }
    
    public void Run()
    {
        byte[] frameArray = ArrayPool<byte>.Shared.Rent(FRAME_SCRATCH_SIZE);
        byte[] payloadArray = ArrayPool<byte>.Shared.Rent(PAYLOAD_BUFFER_SIZE);
        try
        {
            Span<byte> frameScratch = frameArray.AsSpan(0, FRAME_SCRATCH_SIZE);
            Span<byte> payloadBuffer = payloadArray.AsSpan(0, PAYLOAD_BUFFER_SIZE);

            foreach (var entity in _gatewayCacheStore.Sessions)
            {
                var channel = _gatewayCacheStore.GetChannel((int)entity);
                if (channel == null)
                    continue;

                ref var flowState = ref _gatewayCacheStore.FlowStates.Get(entity);

                // True, если канал нужно закрыть после обработки очереди: либо отказ конвейера,
                // либо прохождение всех фаз. Сначала флашим накопленное, потом рвём —
                // иначе send-воркер не допишет в уже completed PipeWriter (детерминированность отправки).
                bool disconnect = false;

                var outbound = new PacketOutbound(channel, _compressor, frameScratch, payloadBuffer);
                PacketHandleResult result;
                while (channel.IncomingPackets.TryDequeue(out var rawPacket))
                {
                    if (flowState.BundleIndex >= _pipeline.BundleCount)
                    {
                        disconnect = true;
                        break;
                    }

                    do
                    {
                        result = _pipeline.TryProcessPacket(entity, rawPacket, ref outbound, ref flowState);

                        if (outbound.Written > 0)
                        {
                            channel.EnqueueOutbound(outbound.WrittenSpan);
                            outbound.Reset();
                        }
                    } while (result == PacketHandleResult.Continue);
                    
                    if (result != PacketHandleResult.Kick) 
                        continue;
                    
                    Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                    disconnect = true;
                    break;
                }

                if (flowState.BundleIndex >= _pipeline.BundleCount)
                {
                    var user = _gatewayCacheStore.UserProfiles.Get(entity);
                    var session = _gatewayCacheStore.Sessions.Get(entity);
                    
                    // Логируем трансфер
                    Logger.Info(LogKey.PacketRealmTransfer, user.Username);
                    
                    _userSessionCacheStore.Transfer(user, session, channel);
                    
                    _gatewayCacheStore.World().DelEntity(entity);
                    _gatewayCacheStore.RemoveChannel(channel);
                    continue;
                }

                if (disconnect)
                    channel.Disconnect();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frameArray);
            ArrayPool<byte>.Shared.Return(payloadArray);
        }
    }
}