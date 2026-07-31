namespace Verstack.Network.Packet;

/// <summary>
/// Результат обработки пакета бандлом. Конвейер (<see cref="PacketPipeline"/>) двигает
/// <see cref="PacketFlowState.StepIndex"/> только при <see cref="Accepted"/>.
/// </summary>
public enum PacketHandleResult
{
    /// <summary>
    /// Пакет принят, шаг пройден — конвейер двигает <see cref="PacketFlowState.StepIndex"/>
    /// (а при исчерпании <see cref="PacketBundle.StepCount"/> — и <see cref="PacketFlowState.BundleIndex"/>).
    /// </summary>
    Accepted,

    /// <summary>
    /// Пакет принят, но конвейер стоит на месте. Для посторонних пакетов, легитимных в фазе,
    /// но не являющихся триггером текущего шага (напр. <c>minecraft:brand</c> в Configuration).
    /// Пакет «проглатывается» без кика и без продвижения.
    /// </summary>
    Ignored,

    /// <summary>
    /// Пакет невалиден для текущего шага — кик клиента.
    /// </summary>
    Kick,
    
    Continue
}