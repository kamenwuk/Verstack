using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Layers.Realm.Shared;
using Verstack.Engine.Lifecycle;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Input.Shared;

/// <summary>
/// Обрабатывает ответ клиента на Keep Alive (serverbound 0x1C). payload (Long) обязан совпасть
/// с тем, что сервер отправил в <c>keep_alive</c> (clientbound 0x2C), и прийти только когда
/// сервер его ждёт (<see cref="KeepAliveInf.IsAwaiting"/>).
///
/// <para>Строгая реакция на любое отклонение — <see cref="PacketHandleResult.Kick"/>: ответ не
/// вовремя, несовпадение payload или отсутствие <see cref="KeepAliveInf"/> у сущности означают
/// битый/читерный клиент. При успехе снимает <see cref="KeepAliveInf.IsAwaiting"/> — после этого
/// <c>KeepAliveSystem</c> в следующем цикле отправки снова свободен.</para>
/// </summary>
internal sealed class KeepAliveResponseBundle : PacketBundle
{
    public override int StepCount => 1;

    private UserSessionCacheStore _userSession = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.REALM];
        _userSession = world.Aspect<UserSessionCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        var reader = packet.CreateReader();
        long payload = reader.ReadLong();

        // Битый payload — недостаточно байт под Long.
        if (!reader.IsValid)
            return PacketHandleResult.Kick;

        // Нет KeepAliveInf — сущность не в.Play-фазе Keep Alive, ответ аномален.
        if (!_userSession.KeepAlives.Has(entity))
            return PacketHandleResult.Kick;

        ref var keepAlive = ref _userSession.KeepAlives.Get(entity);

        // Не ждали ответа — клиент шлёт keep_alive самовольно.
        if (!keepAlive.IsAwaiting)
            return PacketHandleResult.Kick;

        // Payload обязан совпасть с отправленным.
        if (payload != keepAlive.Payload)
            return PacketHandleResult.Kick;

        keepAlive.IsAwaiting = false;

        Logger.Debug(LogKey.PacketPlayKeepAlive, (int)entity, payload);

        return PacketHandleResult.Accepted;
    }
}