using System.Collections.Concurrent;
using Verstack.Engine.Network;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Центральный маршрутизатор (Hub). Связывает асинхронный сетевой слой с синхронным ECS-тиком.
/// Управляет владением каналами: определяет, какому ECS-миру (слою) принадлежит сокет в данный момент.
/// </summary>
public sealed class BridgeHandoffRouter(string defaultScope) : ClientLifecycleHandler
{
    private readonly Dictionary<string, string> _handoffMap = new();
    private readonly Dictionary<NetworkChannel, string> _ownership = new();
    private readonly Lock _lock = new();

    // Очереди новых подключений и трансферов, ожидающие обработки ECS-тиком
    private readonly Dictionary<string, ConcurrentQueue<PendingTransfer>> _pending = new();
    // Очереди отключившихся каналов
    private readonly Dictionary<string, ConcurrentQueue<NetworkChannel>> _disconnected = new();

    /// <summary>
    /// Регистрация цепочки переходов между слоями.
    /// </summary>
    public void AddLayer(string scope, string nextScope)
    {
        _handoffMap[scope] = nextScope;
        _pending[scope] = new ConcurrentQueue<PendingTransfer>();
        _disconnected[scope] = new ConcurrentQueue<NetworkChannel>();
    }

    // Вызывается из TCP-потока при первичном подключении сокета
    protected override void HandleConnect(NetworkChannel channel)
    {
        lock (_lock) { _ownership[channel] = defaultScope; }
        _pending[defaultScope].Enqueue(new PendingTransfer(channel, null));
    }
    
    // Вызывается из TCP-потока при обрыве соединения
    protected override void HandleDisconnect(NetworkChannel channel)
    {
        string targetScope;
        lock (_lock)
        {
            if (!_ownership.TryGetValue(channel, out targetScope)) return;
        }
        _disconnected[targetScope].Enqueue(channel);
    }

    /// <summary>
    /// Передача владения игроком следующему слою. Вызывается текущим слоем, когда политика одобрила трансфер.
    /// </summary>
    internal void TransferToNext(string currentScope, NetworkChannel channel, BridgeHandoffData data)
    {
        if (!_handoffMap.TryGetValue(currentScope, out var nextScope) || nextScope == null)
            throw new InvalidOperationException($"Слой {currentScope} не имеет права передавать канал дальше.");

        lock (_lock)
        {
            if (_ownership.TryGetValue(channel, out var owner) && owner == currentScope)
            {
                _ownership[channel] = nextScope;
            }
            else return;
        }

        _pending[nextScope].Enqueue(new PendingTransfer(channel, data));
    }

    /// <summary>
    /// Извлечение нового игрока из очереди ожидающих обработки.
    /// <remarks>Вызывается <see cref="BridgeIntakeSystem"/></remarks>
    /// </summary>
    internal bool TryDequeuePending(string scope, out NetworkChannel channel, out BridgeHandoffData data)
    {
        if (_pending[scope].TryDequeue(out var transfer))
        {
            channel = transfer.Channel;
            data = transfer.Data;
            return true;
        }

        channel = null!;
        data = null;
        return false;
    }

    /// <summary>
    /// Получение очереди отвалившихся сокетов для системы отключений.
    /// </summary>
    internal ConcurrentQueue<NetworkChannel> GetDisconnected(string scope) => _disconnected[scope];
}