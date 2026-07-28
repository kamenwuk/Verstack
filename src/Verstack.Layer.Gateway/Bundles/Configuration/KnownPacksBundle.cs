using System.Buffers;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Configuration: Known Packs response (0x07) → Feature Flags (0x0C) + Finish Configuration (0x03).
/// Сервер блокирует Configuration до получения serverbound Known Packs (подмножество паков, известных клиенту);
/// читаем только их количество, затем отправляем Feature Flags (<c>["minecraft:vanilla"]</c>) и Finish Configuration.
///
/// TODO: между Known Packs и Feature Flags сюда встанет Registry Data (S→C 0x07) — требует Verstack.NBT.
/// </summary>
internal sealed class KnownPacksBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        // minecraft:brand (C→S 0x02) — может прилететь и на этом шаге.
        if (packet.Id == 0x02)
            return PacketHandleResult.Ignored;
        if (packet.Id != 0x07) // Known Packs (serverbound)
            return PacketHandleResult.Kick;

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        int knownCount = VarInt.Read(ref reader);

        Logger.Debug(LogKey.PacketKnownPacks, knownCount);

        // S→C Feature Flags (0x0C): ["minecraft:vanilla"].
        var ffw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref ffw, 0x0C);                      // Feature Flags ID
        VarInt.Write(ref ffw, 1);                         // 1 flag
        Utf8String.Write(ref ffw, "minecraft:vanilla");
        outbound.Send(ffw.WrittenSpan);

        // S→C Finish Configuration (0x03): полей нет. Отдельный кадр — отдельная Send.
        var fcw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref fcw, 0x03);                      // Finish Configuration ID
        outbound.Send(fcw.WrittenSpan);
        return PacketHandleResult.Accepted;
    }
}