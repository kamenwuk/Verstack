using Leopotam.EcsProto;

namespace Verstack.Network.Packet;

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
    /// <returns>True — шаг пройден. False — кик.</returns>
    public abstract bool TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound);
}