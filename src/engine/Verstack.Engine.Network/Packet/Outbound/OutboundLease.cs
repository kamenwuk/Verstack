using Verstack.Engine.Network.Compression;

namespace Verstack.Engine.Network.Packet.Outbound;

/// <summary>
/// Аренда <see cref="PacketOutbound"/>: единственная точка создания сессии отправки вне
/// входящих-пакетных пайплайнов (<see cref="Pipeline.SequentialPacketPipeline"/>,
/// <see cref="Pipeline.DispatchPacketPipeline"/>).
///
/// <para>Применяется системами <see cref="Leopotam.EcsProto.IProtoRunSystem"/> для отправки пакетов
/// по своей инициативе: chunk-streaming, спавн сущностей, broadcast-рассылки. Пайплайны внутри
/// движка создают <see cref="PacketOutbound"/> своим ходом (internal ctor); внешний код арендует
/// через <see cref="Acquire"/> — инкапсуляция фрейминга сохраняется.</para>
///
/// <para>Арендованный <see cref="PacketOutbound"/> — <c>ref struct</c>, держит буферы из
/// <c>ArrayPool</c>. Caller обязан освободить его через <see cref="PacketOutbound.Dispose"/>
/// (рекомендуется <c>using</c>), иначе буферы утекут. Паттерн:
///
/// <code>
/// using var outbound = OutboundLease.Acquire(channel, _compressor);
/// foreach (var (cx, cz) in chunksToSend) {
///     var w = outbound.Begin();
///     w.WriteVarInt(0x2D);
///     _wire.Write(ref w, column, cx, cz);
///     outbound.Commit(ref w);
/// }   // using → Dispose → Flush + возврат буферов в ArrayPool
/// </code>
/// </para>
/// </summary>
public static class OutboundLease
{
    /// <summary>
    /// Арендовать сессию отправки для канала. Возвращает <see cref="PacketOutbound"/>, который
    /// caller использует для Begin/Commit/.../Flush и обязан освободить через
    /// <see cref="PacketOutbound.Dispose"/> (обычно через <c>using</c>).
    /// </summary>
    public static PacketOutbound Acquire(NetworkChannel channel, IPacketCompressor compressor)
        => new(channel, compressor);
}