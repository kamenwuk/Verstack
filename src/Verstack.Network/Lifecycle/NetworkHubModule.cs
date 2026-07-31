using Verstack.Network.Compression;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

public sealed class NetworkHubModule : IProtoModule
{
    private readonly ZLibPacketDecompressor _zLibPacketDecompressor = new();
    private readonly ZLibPacketCompressor _zLibPacketCompressor = new();
    private readonly TcpNetworkService _tcpNetworkService;
    private readonly NetworkHandoffRouter _handoffRouter;
    
#if DEBUG
    private bool _isInitialized;
#endif
    
    public NetworkHubModule(int port, string defaultScope)
    {
#if DEBUG
        if (string.IsNullOrWhiteSpace(defaultScope))
            throw new ArgumentException("Область по умолчанию (defaultScope) не может быть null или пустой.", nameof(defaultScope));
#endif
        _tcpNetworkService = new TcpNetworkService(port);
        _handoffRouter = new NetworkHandoffRouter(defaultScope);
    }
    
    public void Init(IProtoSystems systems)
    {
        systems
            .AddService(_handoffRouter)
            .AddService(_zLibPacketDecompressor)
            .AddService(_zLibPacketCompressor)
            .AddService(_tcpNetworkService)
            .InitHere<TcpNetworkService>();
#if DEBUG
        _isInitialized = true;
#endif
    }

    public object[] GetServices()
    {
        return
        [
            _zLibPacketCompressor,
            _zLibPacketDecompressor,
            _handoffRouter,
            _tcpNetworkService
        ];
    }

    public void AddLayer(string scope, string nextScope)
    {
#if DEBUG
        if (_isInitialized)
            throw new InvalidOperationException(
                "Нельзя добавлять новые слои после инициализации модуля NetworkHub.");

        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Имя слоя (scope) не может быть null, пустым или состоять только из пробелов.", nameof(scope));
#endif
        _handoffRouter.AddLayer(scope, nextScope);
    }
    
    public IProtoAspect[] Aspects() => [];

    public Type[] Dependencies() => [];
}