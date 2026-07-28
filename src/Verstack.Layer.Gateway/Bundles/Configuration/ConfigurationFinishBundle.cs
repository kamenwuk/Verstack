using System.Buffers;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Core;
using Verstack.Debug;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Bundles;

/// <summary>
/// Шаг Configuration: Acknowledge Finish Configuration (0x03) → Disconnect (0x02) с JSON reason.
/// Play не реализован (REALM пуст), поэтому после Configuration канал закрывается информационным disconnect.
/// После этого шага <see cref="PacketFlowState.BundleIndex"/> выходит за пределы конвейера →
/// <see cref="PacketDispatchSystem"/> закрывает канал (disconnect уходит раньше через flush, по существующему порядку).
/// </summary>
internal sealed class ConfigurationFinishBundle : PacketBundle
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
        // minecraft:brand (C→S 0x02) — последний шанс получить его.
        if (packet.Id == 0x02)
            return PacketHandleResult.Ignored;
        if (packet.Id != 0x03) // Acknowledge Finish Configuration
            return PacketHandleResult.Kick;

        string name = _cache.UserProfiles.Get(entity).Username;
        Logger.Debug(LogKey.PacketConfigurationFinish, name);

        // S→C Disconnect (0x02): Reason = Text Component (VarInt-length + UTF-8 JSON).
        // Тот же формат, что в Status/Login — NBT не нужен.
        var pw = new SpanWriter(outbound.PayloadBuffer);
        VarInt.Write(ref pw, 0x02);                       // Disconnect ID
        Utf8String.Write(ref pw, "{\"text\":\"Фаза Configuration завершена. Play ещё не реализован.\"}");
        outbound.Send(pw.WrittenSpan);
        return PacketHandleResult.Accepted;
    }
}