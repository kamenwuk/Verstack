namespace Verstack.Shared.Bridge;

/// <summary>
/// ECS-компонент. Маркер отключенного игрока. 
/// Сущность видна системам очистки, пока на ней нет BridgeHandoffPending 
/// (т.е. игрок не должен был висеть в необработанном ожидании).
/// </summary>
public readonly struct BridgeClientDisconnected { }