using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Core;

namespace Verstack.Layer.Global;

public sealed class GlobalFeature : VerstackFeature
{
    public override string Scope => WorldScopes.GLOBAL;

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
}

