using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

public sealed class NetworkScopeModule(string scope, string nextScope, NetworkHandoffPolicy networkHandoffPolicy) : IProtoModule
{
    
    public void Init(IProtoSystems systems)
    {
        systems.AddSystem(new NetworkCleanupSystem(scope, nextScope, networkHandoffPolicy))
            .AddSystem(new NetworkDisconnectSystem(scope))
            .AddSystem(new NetworkIntakeSystem(scope));
    }

    public IProtoAspect[] Aspects()
    {
        return [new NetworkHandoffCacheStore()];
    }

    public Type[] Dependencies() => [];
}