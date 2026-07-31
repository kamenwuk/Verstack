using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Lifecycle;

public abstract class ServerFeatureLayer
{
    /// <summary>
    /// Имя ECS-мира, к которому привязан этот Feature. См. <see cref="ServerWorldScopes"/>.
    /// </summary>
    public abstract string Scope { get; }

    public abstract void Init(IProtoSystems systems);

    public abstract ProtoAspectInject[] GetCacheStores();

    protected internal abstract void GetVisibleScopes(ICollection<string> scopes);
}
