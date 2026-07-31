using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using Verstack.Lifecycle;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Nbt;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class ConfigurationFinishBundle : PacketBundle
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
        if (packet.Id == 0x02)
            return PacketHandleResult.Ignored;
        if (packet.Id != 0x03) 
            return PacketHandleResult.Kick;

        // string name = _cache.UserProfiles.Get(entity).Username;
        // Logger.Debug(LogKey.PacketConfigurationFinish, name);
        //
        // Span<NbtFrame> frames = stackalloc NbtFrame[8];
        // Span<byte> nbtBuffer = stackalloc byte[256]; 
        //
        // var nbtWriter = new NbtWriter(nbtBuffer, frames, networked: true);
        // nbtWriter.BeginRootCompound()
        //     .WriteString("text"u8, "Фаза Configuration завершена.\nPlay ещё не реализован."u8)
        //     .WriteString("color"u8, "red"u8)
        //     .WriteBool("bold"u8, true)
        //     .EndCompound();
        //
        // ReadOnlySpan<byte> nbtData = nbtWriter.Finish();
        //
        // var pw = new SpanWriter(outbound.PayloadBuffer);
        // VarInt.Write(ref pw, 0x20); // ID пакета Disconnect (Play)
        //
        // // NBT пишется напрямую БЕЗ префикса длины!
        // nbtData.CopyTo(pw.GetSpan(nbtData.Length));
        // pw.Advance(nbtData.Length);
        //
        // outbound.Send(pw.WrittenSpan);
        string name = _cache.UserProfiles.Get(entity).Username;
        Logger.Debug(LogKey.PacketConfigurationFinish, name);
        
        return PacketHandleResult.Kick;
    }
}