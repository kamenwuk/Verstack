using Verstack.Network.Packet.Writers;
using Verstack.Network.Packet.Readers;
using System.Security.Cryptography;
using Verstack.Layer.Global.User;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;
using Verstack.Lifecycle;
using Verstack.Debug;
using System.Text;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Login Start (0x00): читает имя и UUID клиента, генерирует детерминированный
/// offline-UUID, кладёт профиль на сущность и отправляет Set Compression (если threshold ≥ 0)
/// и Login Success (0x02). Шифрование и Mojang-аутентификация (online mode) вне offline-MVP.
///
/// Тонкий момент протокола: Set Compression уходит ДО <see cref="PacketOutbound.EnableCompression"/>,
/// т.е. несжатым (compression на канале ещё не включена), а Login Success — уже в compressed framing.
/// Это выполняется автоматичеcки: Commit читает threshold канала live.
/// </summary>
internal sealed class LoginStartBundle : PacketBundle
{
    public override int StepCount => 1;

    private GatewayCacheStore _cache = null!;

    public override void Init(IProtoSystems systems)
    {
        var world = systems.NamedWorlds()[ServerWorldScopes.GATEWAY];
        _cache = world.Aspect<GatewayCacheStore>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        if (packet.Id != 0x00) // Login Start
            return PacketHandleResult.Kick;

        var reader = packet.CreateReader();
        ReadOnlyUtf8Span name = reader.ReadString();
        _ = reader.ReadUuid(); // UUID клиента игнорируем: offline-режим генерирует свой

        if (reader.IsFaulted)
            return PacketHandleResult.Kick;
        
        Guid offlineUuid = GenerateOfflinePlayer(name.ToString());
        _cache.UserProfiles.GetOrAdd(entity) = new UserProfile(offlineUuid, name.ToString(), "none");

        Logger.Debug(LogKey.PacketLoginStart, name.ToString());

        // Set Compression (0x03) — несжатый (compression ещё не включена).
        if (ServerConstants.COMPRESSION_THRESHOLD >= 0)
        {
            var setCompression = outbound.Begin();
            setCompression.WriteVarInt(0x03)
                .WriteVarInt(ServerConstants.COMPRESSION_THRESHOLD);
            
            outbound.Commit(ref setCompression);
            outbound.EnableCompression(ServerConstants.COMPRESSION_THRESHOLD);
        }

        // Login Success (0x02) в протоколе 776: Game Profile + Session ID.
        var loginSuccess = outbound.Begin();
        loginSuccess.WriteVarInt(0x02)          // Login Success ID
            .WriteUuid(offlineUuid)             // Game Profile.UUID
            .WriteString(name.AsSpan())                  // Game Profile.Username
            .WriteVarInt(0)                     // Game Profile.Properties count
            .WriteUuid(Guid.NewGuid());         // Session ID (поле 776)
        
        outbound.Commit(ref loginSuccess);
        
        return PacketHandleResult.Accepted;
    }
    
    /// <summary>
    /// Генерирует offline-UUID (версия 3) для имени игрока. Повторяет семантику
    /// <c>java.util.UUID.nameUUIDFromBytes</c> ванильного сервера: MD5 от UTF-8 байтов строки
    /// <c>"OfflinePlayer:" + name</c>, с выставлением version=3 и variant RFC 4122.
    ///
    /// Префикс <c>"OfflinePlayer:"</c> — код самого ванильного сервера (<c>ServerLoginPacketListenerImpl</c>),
    /// не конвенция плагинов. Он даёт детерминированный, воспроизводимый UUID для одного имени —
    /// ключ к данным игрока, единый с другими offline-серверами и перезапусками.
    /// </summary>
    private static Guid GenerateOfflinePlayer(string name)
    {
        // Префикс — чистый ASCII ("OfflinePlayer:" = 14 байт), имя — UTF-8 (до 16 символов по протоколу).
        // Cold path: раз на соединение при логине, простая аллокация честнее ручного stackalloc-трюка.
        const string prefix = "OfflinePlayer:";
        byte[] input = new byte[prefix.Length + Encoding.UTF8.GetByteCount(name)];
        Encoding.ASCII.GetBytes(prefix, 0, prefix.Length, input, 0);
        Encoding.UTF8.GetBytes(name, 0, name.Length, input, prefix.Length);

        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(input, hash);

        // version = 3 (name-based, MD5): верхние 4 бита 7-го байта (index 6) → 0011.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        // variant = RFC 4122: верхние 2 бита 9-го байта (index 8) → 10.
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash, bigEndian: true);
    }
}