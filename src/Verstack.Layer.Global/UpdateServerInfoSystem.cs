using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Core;

namespace Verstack.Layer.Global;

internal sealed class UpdateServerInfoSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly ServerInfoCacheStore _serverInfoCacheStore = null!;
    [DI] private readonly ServerTime _serverTime = null!;
        
    private double _timer;

    public void Init(IProtoSystems systems)
    {
        _timer = 0;
    }

    public void Run()
    {
        _timer += _serverTime.DeltaTime;

        // 20 тиков = 1 секунда (при стандартном TPS сервера)
        if (_timer < ServerConstants.SERVER_INFO_UPDATE_INTERVAL)
            return;

        _timer = 0;
        _serverInfoCacheStore.RebuildIfDirty();
    }
}