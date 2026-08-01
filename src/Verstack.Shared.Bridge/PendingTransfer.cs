using Verstack.Network;

namespace Verstack.Shared.Bridge;

/// <summary>
/// Внутренняя структура маршрутизатора для связки канала и данных трансфера в очереди.
/// </summary>
internal readonly record struct PendingTransfer(NetworkChannel Channel, BridgeHandoffData Data);