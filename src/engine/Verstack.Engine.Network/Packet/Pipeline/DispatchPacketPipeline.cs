using Verstack.Engine.Network.Compression;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;
using Verstack.Engine.Network.Packet.Outbound;

namespace Verstack.Engine.Network.Packet.Pipeline;

/// <summary>
/// Представляет диспетчер пакетов, маршрутизирующий входящие данные в обработчики на основе их идентификатора.
/// </summary>
/// <remarks>
/// <para>
/// В отличие от линейного конвейера, данный пайплайн не имеет внутреннего состояния (stateless). 
/// Он использует словарь <see cref="Dictionary{TKey, TValue}"/> для получения обработчика (<see cref="PacketBundle"/>) 
/// за время O(1), что позволяет обрабатывать пакеты, приходящие в произвольном порядке.
/// </para>
/// <para>
/// <b>Батчинг (Batching):</b> Для всего цикла обработки очереди входящих пакетов создается один экземпляр 
/// <see cref="PacketOutbound"/>. Все ответные пакеты, записанные бандлами в рамках одного вызова 
/// <see cref="ProcessSession"/>, накапливаются в общем буфере и отправляются в сеть единственным вызовом 
/// <see cref="PacketOutbound.Flush"/>. Это минимизирует количество системных вызовов (syscalls) и снижает накладные 
/// расходы на фрейминг TCP.
/// </para>
/// <para>
/// Если во время обработки пакета бандл возвращает <see cref="PacketHandleResult.Kick"/>, конвейер немедленно 
/// сбрасывает накопленные данные в сеть и прерывает обработку оставшихся пакетов в очереди.
/// </para>
/// </remarks>
public sealed class DispatchPacketPipeline
{
    private readonly IPacketCompressor _compressor;
    private readonly Dictionary<int, PacketBundle> _bundleMap;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DispatchPacketPipeline"/>.
    /// </summary>
    /// <param name="systems">Контекст систем для инициализации зависимостей бандлов.</param>
    /// <param name="compressor">Компрессор для сжатия исходящего трафика.</param>
    /// <param name="bundleMap">Словарь, сопоставляющий ID пакета (int) с его обработчиком (<see cref="PacketBundle"/>).</param>
    public DispatchPacketPipeline(IProtoSystems systems, IPacketCompressor compressor, Dictionary<int, PacketBundle> bundleMap)
    {
        _compressor = compressor;
        _bundleMap = bundleMap;

        foreach (var bundle in _bundleMap.Values)
        {
            bundle.Init(systems);
        }
    }

    /// <summary>
    /// Вычитывает и диспетчеризует все пакеты, находящиеся в очереди <see cref="NetworkChannel.IncomingPackets"/>.
    /// </summary>
    /// <param name="entity">Идентификатор сущности, связанной с сетевым каналом.</param>
    /// <param name="channel">Сетевой канал, предоставляющий очередь входящих пакетов.</param>
    /// <returns>
    /// <see cref="PipelineSessionStatus.Ok"/> — если все пакеты успешно обработаны или очередь была пуста;<br/>
    /// <see cref="PipelineSessionStatus.Kick"/> — если один из обработчиков отклонил пакет, сигнализируя о необходимости разрыва соединения.
    /// </returns>
    public PipelineSessionStatus ProcessSession(ProtoEntity entity, NetworkChannel channel)
    {
        if (channel.IncomingPackets.Count == 0)
            return PipelineSessionStatus.Ok;

        var outbound = OutboundLease.Acquire(channel, _compressor);
        try
        {
            while (channel.IncomingPackets.TryDequeue(out var rawPacket))
            {
                if (_bundleMap.TryGetValue(rawPacket.Id, out var bundle))
                {
                    var result = bundle.TryProcess(0, entity, rawPacket, ref outbound);

                    if (result == PacketHandleResult.Kick)
                    {
                        Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                        outbound.Flush();
                        return PipelineSessionStatus.Kick;
                    }
                }
                else
                {
                    //Logger.Warn(LogKey.GatewayPacketRejected, rawPacket.Id);
                }
            }
            
            outbound.Flush();
        }
        finally
        {
            outbound.Dispose();
        }

        return PipelineSessionStatus.Ok;
    }
}