using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

#nullable enable

namespace Verstack.Core;

/// <summary>
/// Помощники сборки ECS-мира из <see cref="VerstackFeature"/>.
/// </summary>
public static class VerstackFeatureExtensions
{
    /// <summary>
    /// Собирает полный <see cref="ProtoSystems"/> для Feature: модули, аспекты, мир,
    /// сервисы и вызов <see cref="VerstackFeature.Init"/>.
    /// Видимость чужих миров (например, Gateway видит Global) настраивается отдельно,
    /// через <see cref="IProtoSystems.AddWorld"/> на готовом результате.
    /// </summary>
    /// <returns>
    /// Готовый <see cref="ProtoSystems"/>, либо <c>null</c>, если у Feature нет аспектов —
    /// Leopotam не создаёт мир без аспектов, поэтому пустой Feature пропускается целиком.
    /// </returns>
    public static ProtoSystems? BuildSystems(this VerstackFeature feature, IEnumerable<object> services)
    {
        var cacheStores = feature.GetCacheStores();
        if (cacheStores is null or { Length: 0 })
            return null;

        var modules = new ProtoModules();
        modules.AddModule(new AutoInjectModule(true));

        foreach (var aspect in cacheStores)
            modules.AddAspect(aspect);

        var world = new ProtoWorld(modules.BuildAspect());
        var systems = new ProtoSystems(world);

        systems.AddWorld(world, feature.Scope);

        foreach (var service in services)
            systems.AddService(service);

        systems.AddModule(modules.BuildModule());

        feature.Init(systems);

        return systems;
    }
}
