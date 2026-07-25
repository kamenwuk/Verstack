using System.Buffers;
using System.Text;
using Verstack.Minecraft.Login;
using Verstack.Protocol;

namespace Verstack.Minecraft.Tests;

/// <summary>
/// LoginStartPacketParser tests: realistic packet, per-field truncation,
/// multibyte username, cursor advancement. Driven through PacketReader +
/// ReadOnlySequence without a socket.
/// </summary>
public class LoginStartPacketParserTests
{
    // Эталонный UUID: 16 байт BE = 0x0123456789ABCDEF0123456789ABCDEF.
    private static readonly byte[] SampleUuidBytes =
    {
        0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
        0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
    };

    [Fact]
    public void TryRead_RealisticLoginStart_ReturnsPacket()
    {
        byte[] body = BuildLoginStartBody("Steve", SampleUuidBytes);

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.True(ok);
        Assert.Equal("Steve", packet.Username);
        Assert.Equal(Uuid.Read(SampleUuidBytes), packet.Uuid);
    }

    [Fact]
    public void PacketId_IsZero()
    {
        Assert.Equal(0x00, LoginStartPacketParser.PACKET_ID);
    }

    [Fact]
    public void TryRead_TruncatedUsernameLength_ReturnsFalse()
    {
        // continuation-байт длины строки, но тела нет.
        byte[] body = { 0xAC };

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.False(ok);
        Assert.Equal(default(LoginStartPacket), packet);
    }

    [Fact]
    public void TryRead_TruncatedUsernameBody_ReturnsFalse()
    {
        // Заявлено "abc" (3), тело "ab".
        byte[] body = { 0x03, (byte)'a', (byte)'b' };

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.False(ok);
        Assert.Equal(default(LoginStartPacket), packet);
    }

    [Fact]
    public void TryRead_MissingUuid_ReturnsFalse()
    {
        // Имя есть, UUID обрезан до 15 байт.
        using var ms = new MemoryStream();
        byte[] name = Encoding.UTF8.GetBytes("Alex");
        ms.WriteByte((byte)name.Length);
        ms.Write(name);
        for (int i = 0; i < 15; i++) ms.WriteByte(0x00);

        var reader = new PacketReader(new ReadOnlySequence<byte>(ms.ToArray()));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.False(ok);
        Assert.Equal(default(LoginStartPacket), packet);
    }

    [Fact]
    public void TryRead_EmptyUsername_ReturnsPacketWithEmptyString()
    {
        byte[] body = BuildLoginStartBody("", SampleUuidBytes);

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.True(ok);
        Assert.Equal("", packet.Username);
    }

    [Fact]
    public void TryRead_MultibyteUsername_DecodesCorrectly()
    {
        string name = "Игрок";
        byte[] body = BuildLoginStartBody(name, SampleUuidBytes);

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        bool ok = LoginStartPacketParser.TryParse(ref reader, out LoginStartPacket packet);

        Assert.True(ok);
        Assert.Equal(name, packet.Username);
    }

    [Fact]
    public void TryRead_ConsumesEntireBody()
    {
        byte[] body = BuildLoginStartBody("Steve", SampleUuidBytes);

        var reader = new PacketReader(new ReadOnlySequence<byte>(body));
        LoginStartPacketParser.TryParse(ref reader, out _);

        Assert.Equal(body.Length, (int)reader.ConsumedBytes);
    }

    private static byte[] BuildLoginStartBody(string username, byte[] uuidBytes)
    {
        using var ms = new MemoryStream();
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];

        byte[] nameBytes = Encoding.UTF8.GetBytes(username);
        int n = VarInt.Encode(nameBytes.Length, buf);
        ms.Write(buf[..n]);
        ms.Write(nameBytes);

        ms.Write(uuidBytes);
        return ms.ToArray();
    }
}