using Verstack.Engine.Network;
using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Абстрактная политика трансфера. Реализуется каждым слоем, который может передавать игроков дальше.
/// Слой сам определяет внутренние условия готовности (например, пройден ли логин, загружен ли профиль).
/// </summary>
public abstract class BridgeHandoffPolicy
{
    protected internal abstract void Init(IProtoSystems systems);
        
    /// <summary>
    /// Вызывается каждый тик для активных сущностей. 
    /// Слой сам решает, готов ли игрок к переходу в следующий мир.
    /// </summary>
    /// <returns>True, если игрок готов, и данные собраны в <paramref name="data"/>.</returns>
    protected internal abstract bool TryTransfer(ProtoEntity entity, NetworkChannel channel, out BridgeHandoffData data);
}