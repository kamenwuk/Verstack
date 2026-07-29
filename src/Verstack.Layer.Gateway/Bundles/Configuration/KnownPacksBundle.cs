using System.Buffers;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Layer.Global;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Configuration: Known Packs response (0x07) → Registry Data (0x07) → Feature Flags (0x0C) + Finish (0x03).
/// Сервер блокирует Configuration до получения serverbound Known Packs (подмножество паков, известных клиенту);
/// читаем только их количество, затем отправляем Registry Data (listing-only, пустые synced-реестры 26.2),
/// Feature Flags (<c>["minecraft:vanilla"]</c>) и Finish Configuration.
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

        // S→C Registry Data (0x07): listing-only — по одному packet на каждый synced-реестр 26.2.
        // 13 variant-реестров требуют ≥1 entry (клиент 26.2 валидирует non-empty): посылаем их
        // canonical entry-ids БЕЗ тел — клиент достаёт тела из bundled-datapack
        // (entry = Identifier + TAG_End 0x00 = Optional<Tag> empty). Остальные 16 уходят пустыми (count=0).
        // Wire-формат 26.2: framed stream-codec, БЕЗ корневого Compound (это формат ≤1.20.x).
        byte[][] syncedIds = VanillaSyncedRegistries.SyncedRegistryIds;
        byte[][][] entryIds = VanillaRegistryEntries.EntryIds;
        for (int i = 0; i < syncedIds.Length; i++)
        {
            byte[] registryId = syncedIds[i];
            byte[][] entries = entryIds[i];

            var rdw = new SpanWriter(outbound.PayloadBuffer);
            VarInt.Write(ref rdw, 0x07);                 // Clientbound Registry Data ID
            Utf8String.Write(ref rdw, registryId);       // Identifier (имя реестра)
            VarInt.Write(ref rdw, entries.Length);       // entries count (0 для listing-only)
            foreach (byte[] entryId in entries)
            {
                Utf8String.Write(ref rdw, entryId);      // Identifier (entry name)
                rdw.GetSpan(1)[0] = 0;                   // Optional<Tag> = empty → TAG_End (0x00)
                rdw.Advance(1);                          //   клиент берёт тело из bundled-datapack
            }
            outbound.Send(rdw.WrittenSpan);
        }

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