using Verstack.Layers.Realm.Session.Physics;
using Verstack.Layers.Realm.Session.Chunks;
using Verstack.Layers.Realm.Shared;
using Verstack.Layers.Realm.Chunks;
using Verstack.Engine.Lifecycle;
using Verstack.Shared.Maths;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Layers.Global;
using Verstack.Layers.Realm.Session.World;

namespace Verstack.Layers.Realm.Session;

/// <summary>
/// Наделяет сущность игровыми компонентами для активной игры в Realm: сеёт присутствие в
/// мире (<see cref="TransformInf"/>) и флаг chunk-observer's (<see cref="ChunkViewportInf"/>).
///
/// <para>Владелец знания «чем отличается свежий вход от возврата игрока» — здесь же ляжет
/// ветвление fresh/returning. Вызывается из <c>HandoffApprovalSystem</c> после завершения
/// Join-конвейера; сессионное состояние (Session/Profile) сеётся самой системой раньше.</para>
/// </summary>
internal sealed class HandoffSeeder
{
    private UserSessionCacheStore _userSession = null!;
    private PhysicsCacheStore _physics = null!;
    private ChunkCacheStore _chunks = null!;
    private WorldObjectCacheStore _worldObjects = null!;

    public void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.REALM];
        _physics = world.Aspect<PhysicsCacheStore>();
        _chunks = world.Aspect<ChunkCacheStore>();
        _userSession = world.Aspect<UserSessionCacheStore>();
        _worldObjects = world.Aspect<WorldObjectCacheStore>();
    }

    public void Seed(ProtoEntity entity)
    {
        // Присутствие в мире + chunk-observer; для returning здесь будет сохранённое состояние.
        _physics.Transforms.Add(entity) = new TransformInf
        {
            Position = new Vector3(8, 65, 8),
            Yaw = SpawnConstants.SPAWN_YAW,
            Pitch = SpawnConstants.SPAWN_PITCH,
            OnGround = true   // игрок спавнится на поверхности
        };
        _chunks.ChunkViewports.Add(entity) = new ChunkViewportInf
        {
            LastCenterX = 0,
            LastCenterZ = 0,
            Radius = ChunkViewportInf.INITIAL_RADIUS
        };
        _userSession.KeepAlives.Add(entity) = new KeepAliveInf();
        
        ref readonly var profile = ref _userSession.UserProfiles.Get(entity);
        _worldObjects.WorldObjects.Add(entity) = new WorldObjectInf
        {
            Kind = WorldObjectKind.User,
            Uuid = profile.Uuid
        };
    }
}