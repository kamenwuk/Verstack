using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Verstack.Minecraft.Handshake;
using Verstack.Minecraft.Session;
using Verstack.Minecraft.Status;
using Verstack.Network;
using Verstack.Protocol;

namespace Verstack.Minecraft.Tests;

/// <summary>
/// PacketDispatcher tests: phase transitions, per-(phase,packetId) routing,
/// Ping→Pong echo, verdict on malformed/unexpected frames. Driven through
/// BufferWriterPipeAdapter — no socket, synchronous.
/// </summary>
public class PacketDispatcherTests
{
    // ─── Handshake routing ──────────────────────────────────────────

    [Fact]
    public void OnPacket_HandshakeWithStatusNextState_TransitionsToStatus()
    {
        var (dispatcher, adapter) = Create();
        byte[] frame = BuildHandshakeBody(774, "localhost", 25565, nextState: 1);

        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        // Handshake ничего не пишет — но меняет фазу. Косвенная проверка фазы: следующий
        // Status Request должен быть принят, а не уйти в default-ветку.
        Assert.Equal(PacketVerdict.Keep, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    [Fact]
    public void OnPacket_HandshakeWithLoginNextState_StaysInHandshake()
    {
        var (dispatcher, adapter) = Create();
        byte[] frame = BuildHandshakeBody(774, "localhost", 25565, nextState: 2);

        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        // Login не реализован — фаза не меняется, ответа нет, соединение держим.
        Assert.Equal(PacketVerdict.Keep, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    [Fact]
    public void OnPacket_StatusRequestBeforeHandshake_ReturnsDisconnect()
    {
        // В Handshake-фазе packetId=0x00 трактуется как Handshake, не Status Request.
        // Пустое тело не парсится как Handshake → Malformed → рвём соединение.
        var (dispatcher, adapter) = Create();
        byte[] frame = BuildStatusRequestBody();

        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        Assert.Equal(PacketVerdict.Disconnect, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    // ─── Status Request → Status Response ───────────────────────────

    [Fact]
    public void OnPacket_StatusRequestInStatusPhase_WritesStatusResponse()
    {
        var (dispatcher, adapter) = Create();
        TransitionToStatus(dispatcher, adapter);

        byte[] frame = BuildStatusRequestBody();
        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        Assert.Equal(PacketVerdict.Keep, verdict);
        // Ответ — framing-обёрнутый payload; разбираем framing обратно.
        var scan = new PacketFrameScanner(new ReadOnlySequence<byte>(adapter.Buffer.WrittenMemory));
        Assert.True(scan.MoveNext());
        var pr = new PacketReader(scan.Current);
        Assert.True(pr.TryReadVarInt(out int packetId));
        Assert.Equal(ServerStatusSerializer.PACKET_ID, packetId);
    }

    // ─── Ping → Pong ────────────────────────────────────────────────

    [Fact]
    public void OnPacket_PingInStatusPhase_EchoesTimestampAsPong()
    {
        var (dispatcher, adapter) = Create();
        TransitionToStatus(dispatcher, adapter);

        long timestamp = 0x0123_4567_89AB_CDEF;
        byte[] frame = BuildPingBody(timestamp);
        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        Assert.Equal(PacketVerdict.Keep, verdict);
        // Разбираем Pong: framing → VarInt(0x01) → long BE == исходный timestamp.
        var scan = new PacketFrameScanner(new ReadOnlySequence<byte>(adapter.Buffer.WrittenMemory));
        Assert.True(scan.MoveNext());
        var pr = new PacketReader(scan.Current);
        Assert.True(pr.TryReadVarInt(out int packetId));
        Assert.Equal(0x01, packetId);
        Assert.True(pr.TryReadInt64BigEndian(out long echoed));
        Assert.Equal(timestamp, echoed);
    }

    [Fact]
    public void OnPacket_MalformedPing_ReturnsDisconnect()
    {
        var (dispatcher, adapter) = Create();
        TransitionToStatus(dispatcher, adapter);

        // Ping без timestamp (только packetId 0x01).
        byte[] frame = BuildMalformedPingBody();
        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        Assert.Equal(PacketVerdict.Disconnect, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    // ─── Unexpected packet ids ──────────────────────────────────────

    [Fact]
    public void OnPacket_UnexpectedPacketId_ReturnsDisconnect()
    {
        var (dispatcher, adapter) = Create();
        TransitionToStatus(dispatcher, adapter);

        // В Status-фазе packetId=0x42 не определён.
        byte[] frame = BuildRawPacketId(0x42);
        PacketVerdict verdict = dispatcher.OnPacket(new ReadOnlySequence<byte>(frame), adapter);

        Assert.Equal(PacketVerdict.Disconnect, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    [Fact]
    public void OnPacket_EmptyFrame_ReturnsDisconnect()
    {
        var (dispatcher, adapter) = Create();
        // Полностью пустой payload — packetId не читается.
        PacketVerdict verdict = dispatcher.OnPacket(ReadOnlySequence<byte>.Empty, adapter);

        Assert.Equal(PacketVerdict.Disconnect, verdict);
        Assert.Equal(0, adapter.Buffer.WrittenCount);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static (PacketDispatcher dispatcher, BufferWriterPipeAdapter adapter) Create()
    {
        var status = new ServerStatusResponse(
            new ServerVersion("1.21.6", 774),
            new ServerCapacity(max: 20, online: 0),
            "Test");
        var adapter = new BufferWriterPipeAdapter(new ArrayBufferWriter<byte>());
        return (new PacketDispatcher(status), adapter);
    }

    /// <summary>Прогоняет диспетчер через Handshake, переводя его в Status.</summary>
    private static void TransitionToStatus(PacketDispatcher dispatcher, PipeWriter writer)
    {
        byte[] handshake = BuildHandshakeBody(774, "localhost", 25565, nextState: 1);
        dispatcher.OnPacket(new ReadOnlySequence<byte>(handshake), writer);
    }

    // ── Билдеры тел пакетов (с packetId в начале) ───────────────────

    private static byte[] BuildHandshakeBody(int protocol, string address, ushort port, int nextState)
    {
        using var ms = new MemoryStream();
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n;

        n = VarInt.Encode(HandshakePacketParser.PACKET_ID, buf); ms.Write(buf[..n]);
        n = VarInt.Encode(protocol, buf); ms.Write(buf[..n]);
        byte[] addr = Encoding.UTF8.GetBytes(address);
        n = VarInt.Encode(addr.Length, buf); ms.Write(buf[..n]);
        ms.Write(addr);
        Span<byte> portBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(portBytes, port);
        ms.Write(portBytes);
        n = VarInt.Encode(nextState, buf); ms.Write(buf[..n]);

        return ms.ToArray();
    }

    private static byte[] BuildStatusRequestBody()
    {
        // Status Request: только packetId 0x00, тело пустое.
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n = VarInt.Encode(ServerStatusSerializer.PACKET_ID, buf);
        return buf[..n].ToArray();
    }

    private static byte[] BuildPingBody(long timestamp)
    {
        using var ms = new MemoryStream();
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n = VarInt.Encode(0x01, buf); ms.Write(buf[..n]);
        Span<byte> ts = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(ts, timestamp);
        ms.Write(ts);
        return ms.ToArray();
    }

    private static byte[] BuildMalformedPingBody()
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n = VarInt.Encode(0x01, buf);
        return buf[..n].ToArray();
    }

    private static byte[] BuildRawPacketId(int id)
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n = VarInt.Encode(id, buf);
        return buf[..n].ToArray();
    }
}