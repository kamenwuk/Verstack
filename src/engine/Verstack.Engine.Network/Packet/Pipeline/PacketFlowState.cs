namespace Verstack.Engine.Network.Packet.Pipeline;

/// <summary>
/// Состояние потока пакетов. Хранит индекс текущего бандла и шаг внутри него.
/// </summary>
public struct PacketFlowState(int bundleIndex, int stepIndex)
{
    public int BundleIndex = bundleIndex;
    public int StepIndex = stepIndex;
}