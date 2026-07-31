using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Network.Lifecycle;

internal sealed class NetworkCleanupSystem(string scope, string nextScope, NetworkHandoffPolicy handoffPolicy) : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly NetworkHandoffRouter _handoffRouter = null!;
    [DI] private readonly NetworkHandoffCacheStore _registry = null!;
    [DI] private readonly ProtoWorld _world = null!;
    
    public void Init(IProtoSystems systems)
    {
        handoffPolicy.Init(systems);
    }
    
    public void Run()
    {
        foreach (var entity in _registry.DisconnectedFilter)
        {
            var channel = _registry.GetChannel((int)entity);
            if (channel != null)
                Logger.Info(LogKey.NetworkChannelDisconnected, channel.RemoteAddress);

            _registry.RemoveChannel(entity, true);
            _world.DelEntity(entity);
        }
        
        if (!string.IsNullOrEmpty(nextScope))
        {
            foreach (var entity in _registry.ConnectedFilter)
            {
                var channel = _registry.GetChannel((int)entity);
                if (channel == null) continue;

                // Вызываем абстрактный метод. Если true — трансфер выполнен внутри политики
                if (handoffPolicy.TryTransfer(entity, channel))
                {
                    _handoffRouter.TransferToNext(scope, channel);
                    _registry.RemoveChannel(entity, false);
                    _world.DelEntity(entity);
                }
            }
        }
    }
}