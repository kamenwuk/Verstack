using Leopotam.EcsProto;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Engine.Network.Packet.Outbound;

namespace Verstack.Engine.Network.Packet.Pipeline;

public abstract class PacketBundle
{
    public int Index { get; internal set; }

    /// <summary>
    /// Число шагов в связке. Pipeline использует это, чтобы понять,
    /// когда шаги закончились и пора перейти к следующему бандлу.
    /// </summary>
    public abstract int StepCount { get; }

    /// <summary>
    /// Вызывается конвейером один раз при сборке. Бандл кэширует
    /// нужные CacheStore из мира
    /// </summary>
    public virtual void Init(IProtoSystems systems) { }

    /// <summary>
    /// Обрабатывает пакет для конкретного шага связки через <paramref name="outbound"/>:
    /// бандл описывает исходящие пакеты (через <see cref="PacketOutbound.Send"/>), а framing
    /// и compression остаются заботой транспорта. Состояние потока меняет Pipeline — бандл к нему не прикасается.
    /// </summary>
    /// <param name="stepIndex">Текущий шаг (0..StepCount-1).</param>
    /// <param name="entity">Сущность подключения (NetworkSession и др.).</param>
    /// <returns>
    /// <see cref="PacketHandleResult.Accepted"/> — шаг пройден, конвейер двигается.
    /// <see cref="PacketHandleResult.Ignored"/> — пакет проглочен без продвижения (посторонний, но легитимный).
    /// <see cref="PacketHandleResult.Kick"/> — пакет невалиден, клиент отключается.
    /// </returns>
    public abstract PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound);
}