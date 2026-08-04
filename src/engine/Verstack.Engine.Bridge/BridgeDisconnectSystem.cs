using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Engine.Bridge;

/// <summary>
/// Система обработки разрывов соединений. 
/// Читает асинхронные события отключения от TCP-слушателя и переносит их в ECS-мир,
/// помечая соответствующие сущности флагом Disconnected.
/// </summary>
internal sealed class BridgeDisconnectSystem(string scope, BridgeHandoffRouter handoffRouter) : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _registry = null!;
        
    public void Run()
    {
        var disconnected = handoffRouter.GetDisconnected(scope);
        while (disconnected.TryDequeue(out var deadChannel))
        {
            Logger.Info(LogKey.NetworkChannelDisconnected, deadChannel.RemoteAddress);
                
            // Помечаем сущность как отключенную. 
            // Дальнейшую работу с ней выполнит BridgeCleanupSystem.
            _registry.MarkDisconnected(deadChannel);
        }
    }
}