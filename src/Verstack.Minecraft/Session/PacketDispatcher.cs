using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using Verstack.Minecraft.Handshake;
using Verstack.Minecraft.Login;
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
    private SessionPhase _phase = SessionPhase.Handshake; // default(Handshake) — стартовая фаза

    // Данные Login Start, сохраняются до Login Success (подэтап 4).
    // До первого Login Start — null / нулевой UUID: игрок не залогинен.
    private string? _loginUsername;
    private Uuid _loginUuid;

    /// <param name="status">Server status data, sent on every Status Request.</param>
    public PacketDispatcher(ServerStatusResponse status)
    {
        _status = status;
    }

    /// <inheritdoc/>
    public PacketVerdict OnPacket(ReadOnlySequence<byte> payload, PipeWriter output)
    {
        var reader = new PacketPayloadReader(payload);

        // packet id читает сам диспетчер — по нему свитчуется роутинг.
        if (!reader.TryReadVarInt(out int packetId))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Frame without packet id — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        switch ((_phase, packetId))
        {
            case (SessionPhase.Handshake, HandshakePacketParser.PACKET_ID):
                return HandleHandshake(ref reader);

            case (SessionPhase.Status, ServerStatusSerializer.PACKET_ID):
                HandleStatusRequest(output);
                return PacketVerdict.Keep;

            case (SessionPhase.Status, PONG_PACKET_ID):
                return HandlePing(ref reader, output);

            case (SessionPhase.Login, LoginStartPacketParser.PACKET_ID):
                return HandleLoginStart(ref reader); 
            
            default:
                Console.WriteLine(
                    $"[{nameof(PacketDispatcher)}] Unexpected packet id 0x{packetId:X2} " +
                    $"in {nameof(SessionPhase)}.{_phase} — dropping connection.");
                return PacketVerdict.Disconnect;
        }
    }

    // Разбирает Handshake и переводит фазу. Login пока не реализован — фаза не
    // меняется, логируется. Остаться в Handshake безопасно: следующий пакет от
    // клиента всё равно уйдёт в default-ветку и порвёт соединение.
    private PacketVerdict HandleHandshake(ref PacketPayloadReader payloadReader)
    {
        if (!HandshakePacketParser.TryParse(ref payloadReader, out HandshakePacket packet))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Handshake — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        switch (packet.NextState)
        {
            case HandshakeNextState.Status:
                _phase = SessionPhase.Status;
                return PacketVerdict.Keep;

            case HandshakeNextState.Login:
                _phase = SessionPhase.Login;
                return PacketVerdict.Keep;
        }

        return PacketVerdict.Keep; // недостижимо — switch покрывает все значения enum
    }

    private void HandleStatusRequest(PipeWriter output)
    {
        _scratch.Clear();
        ServerStatusSerializer.Write(_scratch, in _status);
        PacketFrameWriter.Encode(output, _scratch.WrittenSpan);
    }
    
    private PacketVerdict HandlePing(ref PacketPayloadReader payloadReader, PipeWriter output)
    {
        if (!payloadReader.TryReadInt64BigEndian(out long timestamp))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Ping — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        // Pong payload: [VarInt(PONG_PACKET_ID)][timestamp, 8 байт big-endian].
        _scratch.Clear();
        Span<byte> span = _scratch.GetSpan(VarInt.MAX_SIZE + sizeof(long));
        int written = VarInt.Encode(PONG_PACKET_ID, span);
        BinaryPrimitives.WriteInt64BigEndian(span[written..], timestamp);
        _scratch.Advance(written + sizeof(long));

        PacketFrameWriter.Encode(output, _scratch.WrittenSpan);
        return PacketVerdict.Keep;
    }
    
    private PacketVerdict HandleLoginStart(ref PacketPayloadReader payloadReader)
    {
        if (!LoginStartPacketParser.TryParse(ref payloadReader, out LoginStartPacket packet))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Login Start — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        _loginUsername = packet.Username;
        _loginUuid = packet.Uuid;
        Console.WriteLine($"[{nameof(PacketDispatcher)}] Login Start: '{packet.Username}' ({packet.Uuid}). " +
                          $"Login exchange not implemented — connection will stall.");
        return PacketVerdict.Keep;
    }  
}