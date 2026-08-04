using Verstack.Engine.Network;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Внутренняя структура маршрутизатора для связки канала и данных трансфера в очереди.
/// </summary>
internal readonly record struct PendingTransfer(NetworkChannel Channel, BridgeHandoffData Data);