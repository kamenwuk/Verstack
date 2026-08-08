using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Realm.Session.Physics;
using Verstack.Layers.Realm.Movement;
using Verstack.Engine.Lifecycle;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Debug;
using Verstack.Shared.Maths;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input.Movement;

/// <summary>
/// Принимает ввод перемещения/поворота от клиента (Set Player Position 0x1E, Set Player Position
/// And Rotation 0x1F, Set Player Rotation 0x20) и переводит его в ECS-запрос <see cref="MoveReq"/>.
///
/// <para>Один бандл на три пакета: тела почти идентичны, отличаются набором полей. Зарегистрирован
/// одним экземпляром под тремя id в <see cref="InboundDispatcherSystem"/>.</para>
///
/// <para>За тик может прийти несколько пакетов вперемешку (движение + поворот на месте). Слияние
/// идёт по <see cref="MoveReq.HasPosition"/>/<see cref="MoveReq.HasRotation"/>: пакет 0x20 (только
/// поворот) не затирает Position, накопленную пакетом 0x1F. Итоговый <see cref="MoveReq"/> несёт
/// финальные позицию и поворот.</para>
/// </summary>
internal sealed class AcceptMoveInputBundle : PacketBundle
{
    public override int StepCount => 1;

    private PhysicsCacheStore _physics = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.REALM];
        _physics = world.Aspect<PhysicsCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        var reader = packet.CreateReader();

        // Поля зависят от типа пакета: 0x20 (rotation only) не несёт позиции.
        Vector3 position = default;
        var hasPosition = packet.Id != 0x20;
        if (hasPosition)
        {
            double x = reader.ReadDouble();
            double y = reader.ReadDouble();
            double z = reader.ReadDouble();
            position = new Vector3((float)x, (float)y, (float)z);
        }

        // 0x1E — только позиция; 0x1F и 0x20 несут поворот.
        float yaw = 0f;
        float pitch = 0f;
        var hasRotation = packet.Id == 0x1F || packet.Id == 0x20;
        if (hasRotation)
        {
            yaw = reader.ReadFloat();
            pitch = reader.ReadFloat();
        }

        var onGround = reader.ReadBool();

        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // Слияние по флагам: каждый пакет затирает только свою часть. 0x20 не трогает позицию
        // от 0x1F — иначе игрока телепортировало бы в (0,0,0) при повороте на месте.
        ref var req = ref _physics.MoveReqs.GetOrAdd(entity);
        if (hasPosition)
            req.Position = position;
        if (hasRotation)
        {
            req.Yaw = yaw;
            req.Pitch = pitch;
        }
        req.HasPosition = req.HasPosition || hasPosition;
        req.HasRotation = req.HasRotation || hasRotation;
        req.OnGround = onGround;

        Logger.Debug(LogKey.PacketPlayMove, (int)entity, position.X, position.Y, position.Z);

        return PacketHandleResult.Accepted;
    }
}