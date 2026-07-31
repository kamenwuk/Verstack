using System.Collections.Concurrent;

namespace Verstack.Network.Lifecycle;

internal sealed class NetworkHandoffRouter(string defaultScope)
{
    private readonly Dictionary<string, string> _handoffMap = new();
    private readonly Dictionary<NetworkChannel, string> _ownership = new();
    private readonly Lock _lock = new();

    private readonly Dictionary<string, ConcurrentQueue<NetworkChannel>> _pending = new();
    private readonly Dictionary<string, ConcurrentQueue<NetworkChannel>> _disconnected = new();

    public void AddLayer(string scope, string nextScope)
    {
        _handoffMap[scope] = nextScope;
        _pending[scope] = new ConcurrentQueue<NetworkChannel>();
        _disconnected[scope] = new ConcurrentQueue<NetworkChannel>();
    }

    internal void HandleConnect(NetworkChannel channel)
    {
        // По умолчанию кидаем в первый слой (Gateway)
        // Можно захардкодить, либо передавать дефолтный scope в конструктор
        lock (_lock)
        {
            _ownership[channel] = defaultScope;
        }
        _pending[defaultScope].Enqueue(channel);
    }

    internal void HandleDisconnect(NetworkChannel channel)
    {
        string targetScope;
        lock (_lock)
        {
            if (!_ownership.TryGetValue(channel, out targetScope))
                return;
        }
        
        _disconnected[targetScope].Enqueue(channel);
    }

    public void TransferToNext(string currentScope, NetworkChannel channel)
    {
        if (!_handoffMap.TryGetValue(currentScope, out var nextScope) || nextScope == null)
            throw new InvalidOperationException($"Слой {currentScope} не имеет права передавать канал дальше.");

        lock (_lock)
        {
            if (_ownership.TryGetValue(channel, out var owner) && owner == currentScope)
            {
                _ownership[channel] = nextScope;
            }
            else
            {
                return; // Канал уже отвалился
            }
        }

        _pending[nextScope].Enqueue(channel);
    }

    public ConcurrentQueue<NetworkChannel> GetPending(string scope) => _pending[scope];
    public ConcurrentQueue<NetworkChannel> GetDisconnected(string scope) => _disconnected[scope];
}