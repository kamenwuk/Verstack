using Verstack.Engine.Network.Compression;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Engine.Network;

public sealed class NetworkHubModule(int port, ClientLifecycleHandler clientLifecycleHandler, IPacketDecompressor decompressor, IPacketCompressor compressor)
    : IProtoModule
{
    private readonly TcpNetworkService _tcpNetworkService = new(port, clientLifecycleHandler);


    public void Init(IProtoSystems systems)
    {
        systems
            .AddService(decompressor, typeof(IPacketDecompressor))
            .AddService(compressor, typeof(IPacketCompressor))
            .AddService(_tcpNetworkService)
            .InitHere<TcpNetworkService>();

    }

    public (object value, Type type)[] GetServices()
    {
        return
        [
            (compressor, typeof(IPacketCompressor)),
            (decompressor, typeof(IPacketDecompressor)),
            (_tcpNetworkService, typeof(TcpNetworkService))
        ];
    }
    
    public IProtoAspect[] Aspects() => [];

    public Type[] Dependencies() => [];
}