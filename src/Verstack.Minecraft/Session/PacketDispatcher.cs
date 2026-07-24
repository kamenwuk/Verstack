using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using Verstack.Minecraft.Handshake;
using Verstack.Minecraft.Status;
using Verstack.Network;
using Verstack.Protocol;

namespace Verstack.Minecraft.Session;

/// <summary>
/// Per-connection packet dispatcher: routes each incoming frame by
/// <c>(<see cref="SessionPhase"/>, packet id)</c> to the right handler.
/// Implements <see cref="IPacketHandler"/> — one instance lives for the whole
/// connection, owning its session phase.
/// </summary>
/// <remarks>
/// <para>The session phase is an instance field, isolated per connection. Each
/// <see cref="TcpServer"/> connection gets a fresh dispatcher via
/// <see cref="PacketDispatcherFactory"/>; no shared mutable state between
/// connections.</para>
/// <para>Scratch buffer is a field, not a per-packet local: one allocation per
/// connection, reused across all status frames via <c>Clear()</c>. This keeps
/// the status exchange (3 packets) allocation-free after startup.</para>
/// </remarks>
public sealed class PacketDispatcher : IPacketHandler
{
    /// <summary>Pong packet id in the Status state (echoes the Ping).</summary>
    private const int PONG_PACKET_ID = 0x01;

    private readonly ServerStatusResponse _status;
    private readonly ArrayBufferWriter<byte> _scratch = new();
    private SessionPhase _phase; // default(Handshake) — стартовая фаза

    /// <param name="status">Server status data, sent on every Status Request.</param>
    public PacketDispatcher(ServerStatusResponse status)
    {
        _status = status;
    }

    /// <inheritdoc/>
    public void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output)
    {
        var reader = new PacketReader(payload);

        // packet id читает сам диспетчер — по нему свитчуется роутинг.
        if (!reader.TryReadVarInt(out int packetId))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Frame without packet id — ignoring.");
            return;
        }

        switch ((_phase, packetId))
        {
            case (SessionPhase.Handshake, HandshakePacketParser.PACKET_ID):
                HandleHandshake(ref reader);
                break;

            case (SessionPhase.Status, ServerStatusSerializer.PACKET_ID):
                HandleStatusRequest(output);
                break;

            case (SessionPhase.Status, PONG_PACKET_ID):
                HandlePing(ref reader, output);
                break;

            default:
                Console.WriteLine(
                    $"[{nameof(PacketDispatcher)}] Unexpected packet id 0x{packetId:X2} " +
                    $"in {nameof(SessionPhase)}.{_phase} — ignoring.");
                break;
        }
    }

    // Разбирает Handshake и переводит фазу. Login пока не реализован — фаза не
    // меняется, логируется. Остаться в Handshake безопасно: следующий пакет от
    // клиента всё равно уйдёт в default-ветку и будет проигнорирован.
    private void HandleHandshake(ref PacketReader reader)
    {
        if (!HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Handshake — ignoring.");
            return;
        }

        switch (packet.NextState)
        {
            case HandshakeNextState.Status:
                _phase = SessionPhase.Status;
                break;

            case HandshakeNextState.Login:
                Console.WriteLine(
                    $"[{nameof(PacketDispatcher)}] Login phase not implemented — staying in {nameof(SessionPhase.Handshake)}.");
                break;
        }
    }

    private void HandleStatusRequest(PipeWriter output)
    {
        _scratch.Clear();
        ServerStatusSerializer.Write(_scratch, in _status);
        PacketFraming.Write(output, _scratch.WrittenSpan);
    }

    private void HandlePing(ref PacketReader reader, PipeWriter output)
    {
        if (!reader.TryReadInt64BigEndian(out long timestamp))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Ping — ignoring.");
            return;
        }

        // Pong payload: [VarInt(PONG_PACKET_ID)][timestamp, 8 байт big-endian].
        _scratch.Clear();
        Span<byte> span = _scratch.GetSpan(VarInt.MAX_SIZE + sizeof(long));
        int written = VarInt.Encode(PONG_PACKET_ID, span);
        BinaryPrimitives.WriteInt64BigEndian(span[written..], timestamp);
        _scratch.Advance(written + sizeof(long));

        PacketFraming.Write(output, _scratch.WrittenSpan);
    }
}