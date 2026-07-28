using Verstack.Layer.Gateway;
using Verstack.Layer.Global;
using Verstack.Layer.Realm;
using Leopotam.EcsProto;
using Verstack.Network;
using Verstack.Debug;
using Verstack.Core;
using Verstack.Network.Compression;
using Verstack.Network.Packet;

namespace Verstack.Bootstrap;

public sealed class EntryPoint
{
    private ProtoSystems _globalSystems;
    private ProtoSystems _gatewaySystems;
    // Может быть null, если у Realm нет аспектов — мир не создаётся. Проверяем через ?. перед использованием.
    private ProtoSystems _realmSystems;
        
    private ServerTime _serverTime;
    private TcpNetworkService _tcpNetworkService;
        
    private bool _isRunning;
    private volatile bool _isStopped = false;
    
    private readonly CancellationTokenSource _stopCts = new();
    
    
    public void Start(int port)
    {
        Logger.Info(LogKey.ServerStart, port);
        
        // 1. Инициализация базовых сервисов
        _serverTime = new ServerTime();
        _tcpNetworkService = new TcpNetworkService(new ZLibPacketDecompressor());

        var composer = new ServerComposer(new GlobalFeature(),
                new GatewayFeature(), new RealmFeature())
            .AddService(_serverTime)
            .AddService(_tcpNetworkService)
            .AddService(new ZLibPacketCompressor());

        var (globalSystems, gatewaySystems, realmSystems) = composer.Compose();

        _globalSystems = globalSystems;
        _gatewaySystems = gatewaySystems;
        _realmSystems = realmSystems;

        // 3. Инициализация всех ECS систем
        _globalSystems.Init();
        _gatewaySystems.Init();
        _realmSystems?.Init();

        // 4. Запуск TCP-слушателя в фоновом потоке
        _tcpNetworkService.Start(port);
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
                    _globalSystems.Run();
                    _gatewaySystems.Run();
                    _realmSystems?.Run();
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
        _tcpNetworkService?.Stop();

        // Уничтожаем миры в обратном порядке зависимостей: Realm (видит всех) → Gateway → Global.
        if (_realmSystems is not null)
        {
            var world = _realmSystems.World();
            world.Destroy();
            _realmSystems.Destroy();
        }

        {
            var world = _gatewaySystems.World();
            world.Destroy();
            _gatewaySystems?.Destroy();
        }
        
        {
            var world = _globalSystems.World();
            world.Destroy();
            _globalSystems?.Destroy();
        }
        
        Logger.Info(LogKey.ServerStopped);
    }
}