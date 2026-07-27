using Verstack.Layer.Gateway;
using Verstack.Layer.Global;
using Verstack.Layer.Realm;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Core;

#nullable enable

namespace Verstack.Bootstrap;

internal sealed class ServerComposer(GlobalFeature global, GatewayFeature gateway, RealmFeature realm)
{
    private readonly List<object> _services = [];

    public ServerComposer AddService<TService>(TService service)
        where TService : class
    {
        _services.Add(service);
        return this;
    }

    public (ProtoSystems globalSystems, ProtoSystems gatewaySystems, ProtoSystems? realmSystems) Compose()
    {
        // Сборка миров — одинакова для каждого Feature (видимость своих сервисов и аспекта).
        // Realm может быть null, если у него нет аспектов (мир без аспектов не создаётся).
        // Global/Gateway всегда имеют аспекты — это инвариант проекта, поэтому подавляем nullable-предупреждение.
        ProtoSystems globalSystems  = global.BuildSystems(_services)!;
        ProtoSystems gatewaySystems = gateway.BuildSystems(_services)!;
        ProtoSystems? realmSystems  = realm.BuildSystems(_services);

        // Видимость чужих миров — несимметричная, задаём явно:
        //   GLOBAL виден всем, GATEWAY виден только Realm.
        gatewaySystems.AddWorld(globalSystems.World(), WorldScopes.GLOBAL);
        if (realmSystems is not null)
        {
            realmSystems.AddWorld(globalSystems.World(),  WorldScopes.GLOBAL);
            realmSystems.AddWorld(gatewaySystems.World(), WorldScopes.GATEWAY);
        }
#if DEBUG
        else
        {
            Logger.Debug(LogKey.ComposerRealmSkipped);
        }
#endif

        return (globalSystems, gatewaySystems, realmSystems);
    }
}
