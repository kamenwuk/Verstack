using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Lifecycle;

/// <summary>
/// Помощники сборки ECS-мира из <see cref="ServerFeatureLayer"/>.
/// </summary>
public static class ServerFeatureExtensions
{
    public static ProtoSystems BuildSystems(this ServerFeatureLayer featureLayer, 
        IEnumerable<(object value, Type type)> services,
        params IProtoModule[] addition)
    {
        var cacheStores = featureLayer.GetCacheStores();
        if (cacheStores is null or { Length: 0 })
            throw new Exception();

        var modules = new ProtoModules();
        modules.AddModule(new AutoInjectModule(true));

        foreach (var module in addition)
            modules.AddModule(module);
            
        foreach (var aspect in cacheStores)
            modules.AddAspect(aspect);

        var world = new ProtoWorld(modules.BuildAspect());
        var systems = new ProtoSystems(world);

        // Регистрируем собственный мир под его именем (Scope).
        // Теперь системы внутри этого слоя могут получать к нему доступ по имени.
        systems.AddWorld(world, featureLayer.Scope);

        foreach (var service in services)
            systems.AddService(service.value, service.type);

        systems.AddModule(modules.BuildModule());

        return systems;
    }
}