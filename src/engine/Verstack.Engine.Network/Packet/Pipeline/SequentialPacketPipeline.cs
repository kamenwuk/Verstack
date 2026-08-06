using Verstack.Engine.Network.Compression;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Engine.Network.Packet.Outbound;

namespace Verstack.Engine.Network.Packet.Pipeline;

/// <summary>
/// Представляет линейный конвейер обработки пакетов, требующий строгого соблюдения последовательности шагов.
/// </summary>
/// <remarks>
/// <para>
/// Конвейер поддерживает внутреннее состояние (<see cref="PacketFlowState"/>) для каждого подключения, 
/// гарантируя, что пакеты будут обработаны в строгом порядке, заданном массивом <see cref="PacketBundle"/>.
/// Это означает, что пока текущий бандл не вернет <see cref="PacketHandleResult.Accepted"/> или 
/// <see cref="PacketHandleResult.Continue"/>, конвейер не перейдет к следующему шагу.
/// </para>
/// <para>
/// Если бандл возвращает <see cref="PacketHandleResult.Ignored"/>, состояние конвейера не меняется, 
/// что позволяет пропускать посторонние пакеты, не прерывая процесс согласования (handshake).
/// </para>
/// <para>
/// Для каждого входящего пакета создается собственный <see cref="PacketOutbound"/>, который автоматически 
/// освобождает арендованные буферы после отправки данных.
/// </para>
/// </remarks>
public sealed class SequentialPacketPipeline
{
    /// <summary>
    /// Возвращает количество зарегистрированных бандлов в конвейере.
    /// </summary>
    public int BundleCount => _bundles.Length;

    private readonly IPacketCompressor _compressor;
    private readonly PacketBundle[] _bundles;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SequentialPacketPipeline"/>.
    /// </summary>
    /// <param name="systems">Контекст систем для инициализации зависимостей бандлов.</param>
    /// <param name="compressor">Компрессор, используемый для сжатия исходящих пакетов.</param>
    /// <param name="bundles">Массив бандлов, определяющий строгую последовательность шагов конвейера.</param>
    public SequentialPacketPipeline(IProtoSystems systems, IPacketCompressor compressor, PacketBundle[] bundles)
    {
        _compressor = compressor;
        _bundles = bundles;
        
        for (var idx = 0; idx < _bundles.Length; idx++)
        {
            _bundles[idx].Index = idx;
            _bundles[idx].Init(systems);
        }
    }

    /// <summary>
    /// Обрабатывает все входящие пакеты для указанного сетевого канала, продвигая состояние конвейера.
    /// </summary>
    /// <param name="entity">Идентификатор сущности, связанной с сетевым каналом.</param>
    /// <param name="channel">Сетевой канал, из которого вычитывается очередь пакетов <see cref="NetworkChannel.IncomingPackets"/>.</param>
    /// <param name="state">Текущее состояние потока конвейера (ссылка обновляется по мере обработки).</param>
    /// <returns>
    /// <see cref="PipelineSessionStatus.Ok"/> — если очередь пуста или пакеты успешно обработаны в рамках текущих шагов;<br/>
    /// <see cref="PipelineSessionStatus.Transfer"/> — если достигнут конец массива бандлов (конвейер завершен);<br/>
    /// <see cref="PipelineSessionStatus.Kick"/> — если бандл отклонил пакет (например, нарушение протокола).
    /// </returns>
    public PipelineSessionStatus ProcessSession(ProtoEntity entity, NetworkChannel channel, ref PacketFlowState state)
    {
        while (channel.IncomingPackets.TryDequeue(out var rawPacket))
        {
            if (state.BundleIndex >= _bundles.Length)
                return PipelineSessionStatus.Transfer;

            var outbound = OutboundLease.Acquire(channel, _compressor);
            try
            {
                PacketHandleResult result;
                do
                {
                    result = TryProcessPacket(entity, rawPacket, ref outbound, ref state);
                    outbound.Flush();
                } while (result == PacketHandleResult.Continue);
                
                if (result == PacketHandleResult.Kick) 
                {
                    Logger.Warn(LogKey.GatewayPacketRejected, (int)entity);
                    return PipelineSessionStatus.Kick;
                }
            }
            finally
            {
                outbound.Dispose();
            }
        }

        if (state.BundleIndex >= _bundles.Length)
            return PipelineSessionStatus.Transfer;

        return PipelineSessionStatus.Ok;
    }

    /// <summary>
    /// Пытается обработать одиночный пакет в текущем шаге конвейера.
    /// </summary>
    /// <param name="entity">Сущность подключения.</param>
    /// <param name="packet">Необработанный пакет.</param>
    /// <param name="outbound">Буфер для записи ответных пакетов.</param>
    /// <param name="state">Состояние конвейера.</param>
    /// <returns>Результат попытки обработки.</returns>
    private PacketHandleResult TryProcessPacket(ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound, ref PacketFlowState state)
    {
        if (state.BundleIndex < 0 || state.BundleIndex >= _bundles.Length)
            return PacketHandleResult.Kick;

        var bundle = _bundles[state.BundleIndex];
        var result = bundle.TryProcess(state.StepIndex, entity, packet, ref outbound);

        if (result == PacketHandleResult.Ignored)
            return result;
        
        if (result == PacketHandleResult.Kick)
            return result;
        
        state.StepIndex++;
        if (state.StepIndex >= bundle.StepCount)
        {
            state.BundleIndex++;
            state.StepIndex = 0;
        }
        
        return result;
    }
}