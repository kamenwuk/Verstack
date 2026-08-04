namespace Verstack.Engine.Bridge;

/// <summary>
/// ECS-компонент, обозначающий промежуточное состояние сущности.
/// Суть в том, что слой мира не получит BridgeClientConnected или BridgeClientDisconnected, 
/// пока не одобрит (заберет из очереди) этого игрока.
/// Игрок создан в ECS, но ожидает инициализации специфичными системами текущего слоя.
/// </summary>
public readonly struct BridgeHandoffPending { }