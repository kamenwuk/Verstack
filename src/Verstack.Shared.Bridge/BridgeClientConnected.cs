namespace Verstack.Shared.Bridge;

/// <summary>
/// ECS-компонент. Маркер активного (на "рельсах") состояния игрока. 
/// Сущность видна игровым системам слоя, пока на ней нет BridgeClientDisconnected и BridgeHandoffPending.
/// </summary>
public readonly struct BridgeClientConnected { }