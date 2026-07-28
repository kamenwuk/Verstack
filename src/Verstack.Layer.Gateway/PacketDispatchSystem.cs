using Verstack.Network.Compression;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using System.Buffers;
using Verstack.Debug;

namespace Verstack.Layer.Gateway;

internal sealed class PacketDispatchSystem : IProtoRunSystem
{
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI] private readonly GatewayPacketPipeline _pipeline = null!;
    [DI] private readonly ZLibPacketCompressor _compressor = null!;

    // Буферы арендуются один раз на весь Run, возвращаются в finally (heap → GC-free на cold path).
    //   frameScratch   — framing-выход, contiguous, растёт вправо от 0. Отправляется в канал.
    //   payloadBuffer  — временный буфер под payload текущего пакета; бандл пишет в него через SpanWriter.
    // Размеры покрывают Login (payload ~60 байт, framing ~70) с запасом под Configuration (Registry Data ~KB).
    // Для Play-чанков этого мало — TODO: динамический размер или flush-на-пакет.
    private const int FRAME_SCRATCH_SIZE = 16 * 1024;
    private const int PAYLOAD_BUFFER_SIZE = 4 * 1024;

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
                while (channel.IncomingPackets.TryDequeue(out var rawPacket))
                {
                    if (flowState.BundleIndex >= _pipeline.BundleCount)
                    {
                        disconnect = true;
                        break;
                    }

                    if (!_pipeline.TryProcessPacket(entity, rawPacket, ref outbound, ref flowState))
                    {
                        Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                        disconnect = true;
                        break;
                    }
                }

                if (flowState.BundleIndex >= _pipeline.BundleCount)
                    disconnect = true;

                // Сначала — отправка всего, что бандлы записали в frameScratch.
                if (outbound.Written > 0)
                    channel.EnqueueOutbound(outbound.WrittenSpan);

                // Потом — разрыв, если он был запрошен. Disconnect идемпотентен (CompareExchange-флаг).
                if (disconnect)
                    channel.Disconnect();

                outbound.Reset();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frameArray);
            ArrayPool<byte>.Shared.Return(payloadArray);
        }
    }
}