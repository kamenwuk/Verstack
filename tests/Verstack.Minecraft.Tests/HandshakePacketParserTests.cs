using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Verstack.Minecraft.Handshake;
using Verstack.Protocol;

namespace Verstack.Minecraft.Tests;

/// <summary>
/// HandshakePacketReader tests: realistic handshake, both next states,
/// per-field truncation, invalid next state, UTF-8 address, empty address.
/// Driven through PacketReader + ReadOnlySequence without a socket.
/// </summary>
public class HandshakePacketParserTests
{
    // ─── Realistic handshakes ───────────────────────────────────────

    [Fact]
    public void TryRead_RealisticStatusHandshake_ReturnsPacket()
    {
        // 1.21.6 proto=774, "localhost", port 25565, nextState=Status(1).
        byte[] body = BuildHandshakeBody(774, "localhost", 25565, 1);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.True(ok);
        Assert.Equal(774, packet.ProtocolVersion);
        Assert.Equal("localhost", packet.ServerAddress);
        Assert.Equal((ushort)25565, packet.ServerPort);
        Assert.Equal(HandshakeNextState.Status, packet.NextState);
    }

    [Fact]
    public void TryRead_LoginNextState_ReturnsLoginPacket()
    {
        byte[] body = BuildHandshakeBody(774, "mc.example.com", 25565, 2);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.True(ok);
        Assert.Equal(HandshakeNextState.Login, packet.NextState);
    }

    [Fact]
    public void PacketId_IsZero()
    {
        Assert.Equal(0x00, HandshakePacketParser.PACKET_ID);
    }

    // ─── Per-field truncation ───────────────────────────────────────

    [Fact]
    public void TryRead_TruncatedProtocolVersion_ReturnsFalse()
    {
        // VarInt(774) обрезан до continuation-байта.
        byte[] body = { 0x86 };

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    [Fact]
    public void TryRead_TruncatedAddressLength_ReturnsFalse()
    {
        // VarInt(proto) есть, дальше continuation длины строки.
        byte[] body = { 0x86, 0x06, 0xAC };

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    [Fact]
    public void TryRead_TruncatedAddressBody_ReturnsFalse()
    {
        // Заявлено "abc" (3), тело "ab".
        byte[] body = { 0x86, 0x06, 0x03, (byte)'a', (byte)'b' };

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    [Fact]
    public void TryRead_TruncatedPort_ReturnsFalse()
    {
        // Все поля кроме порта; порт обрезан до 1 байта.
        byte[] body = { 0x86, 0x06, 0x00, 0x63 };

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    [Fact]
    public void TryRead_MissingNextState_ReturnsFalse()
    {
        // Все поля кроме nextState.
        byte[] body = BuildHandshakeBody(774, "ab", 25565, 1, includeNextState: false);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    // ─── Invalid next state ─────────────────────────────────────────

    [Theory]
    [InlineData(0)]   // ниже диапазона протокола
    [InlineData(3)]   // выше диапазона протокола
    [InlineData(-1)]  // отрицательное (sign-extension в 5-м байте VarInt)
    public void TryRead_InvalidNextState_ReturnsFalse(int nextState)
    {
        byte[] body = BuildHandshakeBody(774, "localhost", 25565, nextState);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.False(ok);
        Assert.Equal(default(HandshakePacket), packet);
    }

    // ─── Address edge cases ─────────────────────────────────────────

    [Fact]
    public void TryRead_EmptyAddress_ReturnsPacketWithEmptyString()
    {
        byte[] body = BuildHandshakeBody(774, "", 25565, 1);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.True(ok);
        Assert.Equal("", packet.ServerAddress);
    }

    [Fact]
    public void TryRead_Utf8MultibyteAddress_DecodesCorrectly()
    {
        // "Игрок" — кириллица, мультибайт в UTF-8.
        string address = "Игрок";
        byte[] body = BuildHandshakeBody(774, address, 25565, 1);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        bool ok = HandshakePacketParser.TryParse(ref reader, out HandshakePacket packet);

        Assert.True(ok);
        Assert.Equal(address, packet.ServerAddress);
    }

    // ─── Cursor advancement ─────────────────────────────────────────

    [Fact]
    public void TryRead_ConsumesEntireBody()
    {
        byte[] body = BuildHandshakeBody(774, "localhost", 25565, 1);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(body));
        HandshakePacketParser.TryParse(ref reader, out _);

        // После успешного разбора хвостовых байт быть не должно — handshake
        // не имеет переменного хвоста.
        Assert.Equal(body.Length, (int)reader.ConsumedBytes);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Собирает ТЕЛО пакета Handshake (без packet id): proto, address, port, nextState.
    /// Позволяет тестам оперировать высокоуровневыми полями, а не байтами.
    /// </summary>
    private static byte[] BuildHandshakeBody(int protocolVersion, string address, ushort port, int nextState,
        bool includeNextState = true)
    {
        using var ms = new MemoryStream();
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int n;

        n = VarInt.Encode(protocolVersion, buf);
        ms.Write(buf[..n]);

        byte[] addrBytes = Encoding.UTF8.GetBytes(address);
        n = VarInt.Encode(addrBytes.Length, buf);
        ms.Write(buf[..n]);
        ms.Write(addrBytes);

        Span<byte> portBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(portBytes, port);
        ms.Write(portBytes);

        if (includeNextState)
        {
            n = VarInt.Encode(nextState, buf);
            ms.Write(buf[..n]);
        }

        return ms.ToArray();
    }
}