using Verstack.Layers.Realm.Session.Physics;
using Verstack.Layers.Realm.Session.Chunks;
using Verstack.Layers.Realm.Shared;
using Verstack.Layers.Realm.Input;
using Verstack.Layers.Realm.Join;
using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layers.Realm.Session.World;

namespace Verstack.Layers.Realm;

public sealed class RealmLayer : ServerFeatureLayer
{
    public override string Scope => ServerWorldScopes.REALM;

    public override void Init(IProtoSystems systems)
    {
        systems.AddSystem(new HandoffApprovalSystem())
            .AddSystem(new InboundDispatcherSystem())
            .AddSystem(new KeepAliveSystem())
            .AddSystem(new CommitTransformSystem())
            .AddSystem(new WorldObjectSpawnSystem())
            .AddSystem(new WorldObjectMovementSystem())
            .AddSystem(new WorldObjectDespawnSystem());
    }

    public override ProtoAspectInject[] GetCacheStores()
    {
        return
        [
            new UserSessionCacheStore(),
            new PhysicsCacheStore(),
            new ChunkCacheStore(),
            new WorldObjectCacheStore()
        ];
    }

    protected override void GetVisibleScopes(ICollection<string> scopes)
    {
        scopes.Add(ServerWorldScopes.GLOBAL);
    }

    protected override string GetNextScope() => string.Empty;

    protected override BridgeHandoffPolicy GetHandoffPolicy() => null;
}