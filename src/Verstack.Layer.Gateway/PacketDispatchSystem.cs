using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using System.Buffers;
using Verstack.Debug;

namespace Verstack.Layer.Gateway;

internal sealed class PacketDispatchSystem : IProtoRunSystem
{
    [DI] private readonly GatewayCacheStore _gatewayCacheStore = null!;
    [DI] private readonly GatewayPacketPipeline _pipeline = null!;

    public void Run()
    {
        var tempWriter = new ArrayBufferWriter<byte>();

        foreach (var entity in _gatewayCacheStore.Sessions)
        {
            var channel = _gatewayCacheStore.GetChannel((int)entity);

            if (channel == null)
                continue;

            ref var flowState = ref _gatewayCacheStore.FlowStates.Get(entity);

            while (channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                // Отправляем пакет в конвейер (LoginBundle, ConfigurationBundle и т.д.)
                if (!_pipeline.TryProcessPacket(rawPacket, tempWriter, ref flowState))
                {
                    Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                    channel.Disconnect();
                    break;
                }
            }

            // Всё, что бандлы записали в temp-буфер, — в очередь отправки. Флашит send-воркер.
            if (tempWriter.WrittenCount > 0)
                channel.EnqueueOutbound(tempWriter.WrittenSpan);

            // Очищаем буфер для следующей сущности (переиспользуем аллоцированную память).
            tempWriter.Clear();
        }
    }
}
