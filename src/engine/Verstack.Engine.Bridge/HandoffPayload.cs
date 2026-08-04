using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Структура-обертка, выдаваемая специфичным системам слоя при вызове TryDequeueHandoff.
/// Содержит ECS-сущность и DTO-данные, переданные из предыдущего слоя.
/// </summary>
public readonly struct HandoffPayload(ProtoEntity entity, BridgeHandoffData data)
{
    public readonly ProtoEntity Entity = entity;
    public readonly BridgeHandoffData Data = data;
}