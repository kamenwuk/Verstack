using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Realm.User;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Engine.Lifecycle;
using Verstack.Layers.Realm.Movement;

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

    private UserSessionCacheStore _cache = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.REALM];
        _cache = world.Aspect<UserSessionCacheStore>();
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
        _cache.MoveReqs.GetOrAdd(entity) = new MoveReq
        {
            X = x, Y = y, Z = z,
            Yaw = yaw, Pitch = pitch,
            HasRotation = hasRotation
        };

        Logger.Debug(LogKey.PacketPlayMove, (int)entity, x, y, z);

        return PacketHandleResult.Accepted;
    }
}