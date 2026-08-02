using Verstack.Network.Packet.Writers;
using Verstack.Network.Packet.Readers;
using Verstack.Layer.Global.User;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Debug;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Configuration: Client Information (0x00) → Known Packs (0x0E). Читает locale
/// (остальные поля Client Information игнорируем — поток предфреймован, дочитывать до конца
/// не нужно, как в <see cref="LoginStartBundle"/>), сохраняет его в <see cref="UserProfile"/>
/// и отправляет S→C Known Packs с одним паком <c>minecraft:core@26.2</c>.
/// Client Information — первый пакет клиента в Configuration.
/// </summary>
internal sealed class ClientInformationBundle : PacketBundle
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
        // minecraft:brand (C→S 0x02) — клиент шлёт проактивно, не является триггером шага.
        if (packet.Id == 0x02)
            return PacketHandleResult.Ignored;
        if (packet.Id != 0x00) // Client Information
            return PacketHandleResult.Kick;

        var reader = packet.CreateReader();
        //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
        ReadOnlyUtf8Span locale = reader.ReadString();

        if (reader.IsFaulted)
            return PacketHandleResult.Kick;
        
        // Сохраняем locale в профиль — пригодится в Play (локализация серверных сообщений).
        ref var profile = ref _cache.UserProfiles.Get(entity);
        profile = new UserProfile(profile.Uuid, profile.Username, locale.ToString());
        Logger.Debug(LogKey.PacketClientInformation, profile.Locale);

        // S→C Known Packs (0x0E): один пак minecraft:core@26.2.

        var writer = outbound.Begin();
        {
            // Known Packs ID
            writer.WriteVarInt(0x0E)  
                // 1 pack
                .WriteVarInt(1)
                // namespace
                .WriteString("minecraft"u8)
                // id
                .WriteString("core"u8)
                // version
                .WriteString("26.2"u8);
        }
        outbound.Commit(ref writer);
        
        return PacketHandleResult.Accepted;
    }
}