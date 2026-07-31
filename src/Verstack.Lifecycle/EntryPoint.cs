using Verstack.Network.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Lifecycle;

public sealed class EntryPoint
{
    private ServerTime _serverTime = null!;
    private ProtoSystems[] _layers = null!;
        
    private bool _isRunning;
    private volatile bool _isStopped = false;
    
    private readonly CancellationTokenSource _stopCts = new();

    public void Start(int port, ServerFeatureLayer globalLayer, params ServerFeatureLayer[] layers)
    {
        Logger.Info(LogKey.ServerStart, port);

        var netHubModule = new NetworkHubModule(port, ServerWorldScopes.GATEWAY);
        
        netHubModule.AddLayer(ServerWorldScopes.GATEWAY, ServerWorldScopes.REALM);
        netHubModule.AddLayer(ServerWorldScopes.REALM, null);
        // 1. Инициализация базовых сервисов
        _serverTime = new ServerTime();

        var composer = new ServerComposer(globalLayer, netHubModule, layers)
            .AddService(_serverTime);

        _layers = composer.Compose();

        foreach (var layer in _layers)
        {
            layer.Init();
        }

        // 4. Запуск TCP-слушателя в фоновом потоке
        Logger.Info(LogKey.ServerStarted);
        // 5. Запуск главного цикла (Tick Loop)
        _isRunning = true;
        RunMainLoop();
    }

    private void RunMainLoop()
    {
        try
        {
            while (_isRunning)
            {
                try
                {
                    foreach (var layer in _layers)
                        layer.Run();
                }
                catch (Exception ex)
                {
                    Logger.Error(LogKey.ServerTickFailed, ex);
                }

                _serverTime.Update();

                double sleepTime = ServerConstants.TICK_INTERVAL - _serverTime.DeltaTime;
                if (sleepTime > 0)
                {
                    // Спим до следующего тика, но мгновенно просыпаемся по сигналу остановки.
                    _stopCts.Token.WaitHandle.WaitOne((int)(sleepTime * 1000));
                }
            }
        }
        finally
        {
            _stopCts.Dispose();
        }
    }

    public void Stop()
    {
        if (_isStopped) return;
        _isStopped = true;
        Logger.Warn(LogKey.ServerStop);
        _isRunning = false;
        _stopCts.Cancel();

        foreach (var layer in _layers)
        {
            var world = layer.World();
            world.Destroy();
            layer.Destroy();
        }

        _layers = [];
        
        Logger.Info(LogKey.ServerStopped);
    }
}