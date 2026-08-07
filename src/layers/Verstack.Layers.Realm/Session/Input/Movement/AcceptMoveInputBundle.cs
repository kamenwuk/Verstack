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
/// Принимает ввод перемещения от клиента (Set Player Position 0x1E и Set Player Position
/// And Rotation 0x1F) и переводит его в ECS-запрос <see cref="MoveRequestComponent"/>.
///
/// <para>Один бандл на оба пакета: тела почти идентичны, отличаются только наличием yaw/pitch.
/// Зарегистрирован одним экземпляром под двумя id в <see cref="InboundDispatcherSystem"/> —
/// <c>DispatchPacketPipeline</c> вызывает <see cref="PacketBundle.Init"/> по <c>Values</c>,
/// поэтому двойной инициализации не будет.</para>
///
/// <para>За тик может прийти несколько пакетов: последний перетирает предыдущие через
/// <c>GetOrAdd</c> — нас интересует финальная позиция игрока в конце тика.</para>
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

        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();

        // Пакет 0x1F несёт дополнительно yaw/pitch; 0x1E — только позицию.
        float yaw = 0f;
        float pitch = 0f;
        var hasRotation = packet.Id == 0x1F;
        if (hasRotation)
        {
            yaw = reader.ReadFloat();
            pitch = reader.ReadFloat();
        }

        _ = reader.ReadBool(); // onGround — пока не используется, читаем для корректности потока.

        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // Последний пакет за тик перетирает предыдущие: важна финальная позиция.
        _physics.MoveReqs.GetOrAdd(entity) = new MoveReq
        {
            Position = new Vector3((float)x, (float)y, (float)z),
            Yaw = yaw,
            Pitch = pitch,
            HasRotation = hasRotation
        };

        Logger.Debug(LogKey.PacketPlayMove, (int)entity, x, y, z);

        return PacketHandleResult.Accepted;
    }
}