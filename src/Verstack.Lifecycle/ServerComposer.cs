using Leopotam.EcsProto;


namespace Verstack.Lifecycle;

internal sealed class ServerComposer(params ServerFeatureLayer[] layers)
{
    private readonly List<object> _services = [];

    public ServerComposer AddService<TService>(TService service)
        where TService : class
    {
        _services.Add(service);
        return this;
    }

    public ProtoSystems[] Compose()
    {
        var count = layers.Length;
        var systems = new ProtoSystems[count];
        var worldLookup = new Dictionary<string, ProtoWorld>(count);
            
        // Переиспользуемый список для零 аллокаций
        var requestedScopes = new HashSet<string>(4);

        // Фаза 1: Создание миров
        for (var idx = 0; idx < count; idx++)
        {
            var layer = layers[idx];
            var sys = layer.BuildSystems(_services);
            systems[idx] = sys;
            worldLookup[layer.Scope] = sys.World();
        }

        // Фаза 2: Настройка видимости миров
        for (var idx = 0; idx < count; idx++)
        {
            var layer = layers[idx];
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
                    Console.WriteLine(layer.Scope + " ~" + scope);
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
        {
            layers[idx].Init(systems[idx]);
        }

        return systems;
    }
}
