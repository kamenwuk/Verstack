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
    private SessionPhase _phase = SessionPhase.Handshake;

    // Данные Login Start, сохраняются до Login Success (подэтап 4).
    private string? _loginUsername;
    private Uuid _loginUuid;

    // Настройки сжатия
    private readonly IPacketCompressor? _compressor;
    private readonly int _compressionThreshold;
    private bool _isCompressionEnabled;

    /// <summary>
    /// Creates a new dispatcher for a connection.
    /// </summary>
    /// <param name="status">Server status data, sent on every Status Request.</param>
    /// <param name="compressor">Compressor instance. If null, compression is disabled.</param>
    /// <param name="compressionThreshold">Minimum payload size to compress. Default 256 bytes.</param>
    public PacketDispatcher(ServerStatusResponse status, IPacketCompressor? compressor = null, int compressionThreshold = 256)
    {
        _status = status;
        _compressor = compressor;
        _compressionThreshold = compressionThreshold;
    }

    /// <inheritdoc/>
    public PacketVerdict OnPacket(ReadOnlySequence<byte> payload, PipeWriter output)
    {
        var reader = new PacketPayloadReader(payload);

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
                return HandleLoginStart(ref reader, output); 
            
            default:
                Console.WriteLine(
                    $"[{nameof(PacketDispatcher)}] Unexpected packet id 0x{packetId:X2} " +
                    $"in {nameof(SessionPhase)}.{_phase} — dropping connection.");
                return PacketVerdict.Disconnect;
        }
    }

    // Централизованная отправка пакета: учитывает флаг сжатия.
    private void WritePacket(PipeWriter output, ReadOnlySpan<byte> payload)
    {
        if (_isCompressionEnabled && _compressor != null)
        {
            PacketFrameWriter.Encode(output, payload, _compressor, _compressionThreshold);
        }
        else
        {
            PacketFrameWriter.Encode(output, payload);
        }
    }

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

        return PacketVerdict.Keep; 
    }

    private void HandleStatusRequest(PipeWriter output)
    {
        _scratch.Clear();
        ServerStatusSerializer.Write(_scratch, in _status);
        // Статус всегда идёт без сжатия (флаг _isCompressionEnabled ещё false)
        WritePacket(output, _scratch.WrittenSpan);
    }
    
    private PacketVerdict HandlePing(ref PacketPayloadReader payloadReader, PipeWriter output)
    {
        if (!payloadReader.TryReadInt64BigEndian(out long timestamp))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Ping — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        _scratch.Clear();
        Span<byte> span = _scratch.GetSpan(VarInt.MAX_SIZE + sizeof(long));
        int written = VarInt.Encode(PONG_PACKET_ID, span);
        BinaryPrimitives.WriteInt64BigEndian(span[written..], timestamp);
        _scratch.Advance(written + sizeof(long));

        WritePacket(output, _scratch.WrittenSpan);
        return PacketVerdict.Keep;
    }
    
    private PacketVerdict HandleLoginStart(ref PacketPayloadReader payloadReader, PipeWriter output)
    {
        if (!LoginStartPacketParser.TryParse(ref payloadReader, out LoginStartPacket packet))
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Malformed Login Start — dropping connection.");
            return PacketVerdict.Disconnect;
        }

        _loginUsername = packet.Username;
        _loginUuid = packet.Uuid;

        // Если компрессор настроен — отправляем Set Compression ДО включения флага сжатия.
        if (_compressor != null)
        {
            _scratch.Clear();
            SetCompressionSerializer.Write(_scratch, _compressionThreshold);
            
            // ВАЖНО: Set Compression отправляем строго БЕЗ сжатия, напрямую через PacketFrameWriter
            PacketFrameWriter.Encode(output, _scratch.WrittenSpan, compressor: null, compressionThreshold: -1);
            
            // Включаем сжатие для последующих пакетов
            _isCompressionEnabled = true;
            
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Login Start: '{packet.Username}' ({packet.Uuid}). " +
                              $"Sent Set Compression (threshold={_compressionThreshold}).");
        }
        else
        {
            Console.WriteLine($"[{nameof(PacketDispatcher)}] Login Start: '{packet.Username}' ({packet.Uuid}). " +
                              $"Compression disabled. Waiting for Login Success implementation.");
        }

        // TODO: Будущая отправка Login Success пойдёт через WritePacket, и она автоматически сожмётся, если нужно.
        return PacketVerdict.Keep;
    }  
}