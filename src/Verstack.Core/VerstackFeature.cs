using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Core;

public abstract class VerstackFeature
{
    /// <summary>
    /// Имя ECS-мира, к которому привязан этот Feature. См. <see cref="WorldScopes"/>.
    /// </summary>
    public abstract string Scope { get; }

    public abstract void Init(IProtoSystems systems);

    public abstract ProtoAspectInject[] GetCacheStores();
}
