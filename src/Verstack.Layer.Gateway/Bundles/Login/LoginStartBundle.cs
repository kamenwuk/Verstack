using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Verstack.Layer.Global;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Core;
using Verstack.Debug;
using System.Buffers;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Login Start (0x00): читает имя и UUID клиента, генерирует детерминированный
/// offline-UUID, кладёт профиль на сущность и отправляет Set Compression (если threshold ≥ 0)
/// и Login Success (0x02). Шифрование и Mojang-аутентификация (online mode) вне offline-MVP.
///
/// Тонкий момент протокола: Set Compression уходит ДО <see cref="PacketOutbound.EnableCompression"/>,
/// т.е. несжатым (compression на канале ещё не включена), а Login Success — уже в compressed framing.
/// Это выполняется автоматичеcки: Send читает threshold канала live.
/// </summary>
internal sealed class LoginStartBundle : PacketBundle
{
    public override int StepCount => 1;

    private GatewayCacheStore _cache = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[WorldScopes.GATEWAY];
        _cache = world.Aspect<GatewayCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x00) // Login Start
            return PacketHandleResult.Kick;

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        string name = Utf8String.Read(ref reader);
        _ = Uuid.Read(ref reader); // UUID клиента игнорируем: offline-режим генерирует свой

        Guid offlineUuid = Uuid.GenerateOfflinePlayer(name);
        _cache.UserProfiles.GetOrAdd(entity) = new UserProfile(offlineUuid, name);

        Logger.Debug(LogKey.PacketLoginStart, name);

        // Set Compression (0x03) — несжатый (compression ещё не включена).
        if (ServerConstants.COMPRESSION_THRESHOLD >= 0)
        {
            var scw = new SpanWriter(outbound.PayloadBuffer);
            VarInt.Write(ref scw, 0x03);                                  // Set Compression ID
            VarInt.Write(ref scw, ServerConstants.COMPRESSION_THRESHOLD);
            outbound.Send(scw.WrittenSpan);
            outbound.EnableCompression(ServerConstants.COMPRESSION_THRESHOLD);
        }

        // Login Success (0x02) в протоколе 776: Game Profile + Session ID.
        var pw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref pw, 0x02);                       // Login Success ID
        Uuid.Write(ref pw, offlineUuid);                  // Game Profile.UUID
        Utf8String.Write(ref pw, name);                   // Game Profile.Username
        VarInt.Write(ref pw, 0);                          // Game Profile.Properties count
        Uuid.Write(ref pw, Guid.NewGuid());               // Session ID (поле 776)
        outbound.Send(pw.WrittenSpan);
        return PacketHandleResult.Accepted;
    }
}