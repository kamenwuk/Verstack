using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Lifecycle;
using Verstack.Network.Lifecycle;

namespace Verstack.Layer.Global;

public sealed class GlobalLayer : ServerFeatureLayer
{
    public override string Scope => ServerWorldScopes.GLOBAL;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new UpdateServerInfoSystem());
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return
        [
            new ServerInfoCacheStore("A Minecraft Server", 100, "26.2", 776)
        ];
    }

    protected override void GetVisibleScopes(ICollection<string> scopes) { }

    protected override string GetNextScope() => string.Empty;
    protected override NetworkHandoffPolicy GetHandoffPolicy() => null;
}

