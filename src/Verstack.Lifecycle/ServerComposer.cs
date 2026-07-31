using Verstack.Network.Lifecycle;
using Leopotam.EcsProto;

namespace Verstack.Lifecycle;

internal sealed class ServerComposer
{
    private readonly NetworkHubModule _networkHubModule;
    private readonly ServerFeatureLayer[] _layers;
    
    public ServerComposer(ServerFeatureLayer global, NetworkHubModule networkHubModule, params ServerFeatureLayer[] layers)
    {
        _networkHubModule = networkHubModule;
        _layers = new ServerFeatureLayer[1 + layers.Length];
        _layers[0] = global;
        for (var idx = 0; idx < layers.Length; idx++)
            _layers[idx + 1] = layers[idx];
    }
    
    private readonly List<object> _services = [];

    public ServerComposer AddService<TService>(TService service)
        where TService : class
    {
        _services.Add(service);
        return this;
    }

    public ProtoSystems[] Compose()
    {
        var count = _layers.Length;
        var systems = new ProtoSystems[count];
        var worldLookup = new Dictionary<string, ProtoWorld>(count);
        
        // Переиспользуемый список для零 аллокаций
        var requestedScopes = new HashSet<string>(4);

        // Фаза 1: Создание миров
        {
            var layer = _layers[0];
            var sys = layer.BuildSystems(_services, _networkHubModule);
            systems[0] = sys;
            worldLookup[layer.Scope] = sys.World();

            foreach (var service in _networkHubModule.GetServices())
                _services.Add(service);
        }
        {
            for (var idx = 1; idx < count; idx++)
            {
                var layer = _layers[idx];
                var sys = layer.BuildSystems(_services,
                    new NetworkScopeModule(layer.Scope, layer.GetNextScope(),
                        layer.GetHandoffPolicy()));
                systems[idx] = sys;
                worldLookup[layer.Scope] = sys.World();
            }
        }

        // Фаза 2: Настройка видимости миров
        for (var idx = 0; idx < count; idx++)
        {
            var layer = _layers[idx];
            var sys = systems[idx];

            requestedScopes.Clear();
            
            // Слой сам запрашивает нужные ему миры
            layer.GetVisibleScopes(requestedScopes);

            // Регистрируем запрошенные миры
            foreach (var scope in requestedScopes)
            {
                if (scope == layer.Scope) continue; // Свой мир уже есть
                

                if (worldLookup.TryGetValue(scope, out var foreignWorld))
                {
                    sys.AddWorld(foreignWorld, scope);
                }
                else
                {
                    throw new InvalidOperationException($"Слой '{layer.Scope}' требует доступ к миру '{scope}', но такой слой не был зарегистрирован.");
                }
            }
        }

        // Фаза 3: Инициализация слоев
        for (var idx = 0; idx < count; idx++)
            _layers[idx].Init(systems[idx]);

        return systems;
    }
}
