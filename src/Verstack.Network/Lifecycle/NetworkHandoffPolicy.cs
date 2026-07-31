using Leopotam.EcsProto;

namespace Verstack.Network.Lifecycle;

public abstract class NetworkHandoffPolicy
{
    protected internal abstract void Init(IProtoSystems systems);
    
    protected internal abstract bool TryTransfer(ProtoEntity entity, NetworkChannel channel);
}