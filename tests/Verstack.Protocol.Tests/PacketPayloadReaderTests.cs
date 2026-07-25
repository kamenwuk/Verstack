using System.Buffers;
using System.Text;
using Verstack.Protocol;

namespace Verstack.Protocol.Tests;

/// <summary>
/// PacketReader tests: each wire primitive (VarInt, big-endian ushort/long,
/// length-prefixed string), partial/malformed cases, multi-segment reads,
/// and a mixed sequential read mirroring the Handshake layout.
/// Driven through Span/Sequence without a socket.
/// </summary>
public class PacketPayloadReaderTests
{
    // ─── TryReadVarInt ──────────────────────────────────────────────

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0)]
    [InlineData(new byte[] { 0x7F }, 127)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128)]
    [InlineData(new byte[] { 0xAC, 0x02 }, 300)]
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16384)]
    public void TryReadVarInt_KnownBytes_ReturnsValue(byte[] bytes, int expected)
    {
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadVarInt(out int value);

        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(new byte[] { 0xAC })]            // continuation есть, 2-го байта нет
    [InlineData(new byte[] { 0x80, 0x80 })]       // continuation есть, 3-го байта нет
    public void TryReadVarInt_PartialVarInt_ReturnsFalse(byte[] bytes)
    {
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadVarInt(out int value);

        Assert.False(ok);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryReadVarInt_Malformed5Bytes_ReturnsFalse()
    {
        // 5 байт continuation: VarInt не закрылся → битый.
        byte[] bytes = { 0x80, 0x80, 0x80, 0x80, 0x80 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadVarInt(out int value);

        Assert.False(ok);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryReadVarInt_MultiSegment_ReadsAcrossBoundary()
    {
        // VarInt(300) = [0xAC, 0x02], разрезан между сегментами.
        var sequence = TestSequenceBuilder.BuildSegmented(
            new byte[] { 0xAC },
            new byte[] { 0x02 });
        var reader = new PacketPayloadReader(sequence);

        bool ok = reader.TryReadVarInt(out int value);

        Assert.True(ok);
        Assert.Equal(300, value);
    }

    // ─── TryReadUShortBigEndian ────────────────────────────────────

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00 }, (ushort)0)]
    [InlineData(new byte[] { 0x00, 0xFF }, (ushort)255)]
    [InlineData(new byte[] { 0x01, 0x00 }, (ushort)256)]
    [InlineData(new byte[] { 0xFF, 0xFF }, (ushort)65535)]
    public void TryReadUShortBigEndian_KnownBytes_ReturnsValue(byte[] bytes, ushort expected)
    {
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadUShortBigEndian(out ushort value);

        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryReadUShortBigEndian_IsBigEndian()
    {
        // 0x01 0x00 в big-endian = 256, в little-endian было бы 1.
        byte[] bytes = { 0x01, 0x00 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadUShortBigEndian(out ushort value);

        Assert.True(ok);
        Assert.Equal((ushort)256, value);
    }

    [Fact]
    public void TryReadUShortBigEndian_Partial_ReturnsFalse()
    {
        // Одного байта мало для ushort.
        byte[] bytes = { 0x01 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadUShortBigEndian(out ushort value);

        Assert.False(ok);
        Assert.Equal((ushort)0, value);
    }

    // ─── TryReadInt64BigEndian ─────────────────────────────────────

    [Fact]
    public void TryReadInt64BigEndian_KnownValue_ReturnsValue()
    {
        // 1 в big-endian: 7 нулей + 0x01.
        byte[] bytes = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadInt64BigEndian(out long value);

        Assert.True(ok);
        Assert.Equal(1L, value);
    }

    [Fact]
    public void TryReadInt64BigEndian_IsBigEndian()
    {
        // long.MaxValue — все значащие биты.
        byte[] bytes = { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadInt64BigEndian(out long value);

        Assert.True(ok);
        Assert.Equal(long.MaxValue, value);
    }

    [Fact]
    public void TryReadInt64BigEndian_Partial_ReturnsFalse()
    {
        byte[] bytes = { 0x00, 0x00, 0x00 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadInt64BigEndian(out long value);

        Assert.False(ok);
        Assert.Equal(0L, value);
    }

    // ─── TryReadString ──────────────────────────────────────────────

    [Fact]
    public void TryReadString_Ascii_ReturnsValue()
    {
        // [VarInt(5)] "Hello"
        byte[] bytes = { 0x05, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadString(out string? value);

        Assert.True(ok);
        Assert.Equal("Hello", value);
    }

    [Fact]
    public void TryReadString_Empty_ReturnsEmpty()
    {
        // [VarInt(0)] — префикс без тела.
        byte[] bytes = { 0x00 };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadString(out string? value);

        Assert.True(ok);
        Assert.Equal("", value);
    }

    [Fact]
    public void TryReadString_MultibyteUtf8_DecodesCorrectly()
    {
        // "Тест" — кириллица, 2 байта на символ в UTF-8 = 8 байт.
        string original = "Тест";
        byte[] utf8 = Encoding.UTF8.GetBytes(original);
        byte[] bytes = new byte[1 + utf8.Length];
        bytes[0] = (byte)utf8.Length; // для коротких длин VarInt = один байт
        Array.Copy(utf8, 0, bytes, 1, utf8.Length);
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadString(out string? value);

        Assert.True(ok);
        Assert.Equal(original, value);
    }

    [Fact]
    public void TryReadString_PartialBody_ReturnsFalse()
    {
        // [VarInt(10)] "Hello" — заявлено 10, есть 5.
        byte[] bytes = { 0x0A, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadString(out string? value);

        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public void TryReadString_MissingLengthPrefix_ReturnsFalse()
    {
        // continuation VarInt, длина не закрыта.
        byte[] bytes = { 0xAC };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool ok = reader.TryReadString(out string? value);

        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public void TryReadString_MultiSegment_ReadsAcrossBoundary()
    {
        // [VarInt(5)] "Hello", тело разрезано между сегментами.
        var sequence = TestSequenceBuilder.BuildSegmented(
            new byte[] { 0x05, (byte)'H', (byte)'e' },
            new byte[] { (byte)'l', (byte)'l', (byte)'o' });
        var reader = new PacketPayloadReader(sequence);

        bool ok = reader.TryReadString(out string? value);

        Assert.True(ok);
        Assert.Equal("Hello", value);
    }

    // ─── Sequential reads (Handshake-like layout) ───────────────────

    [Fact]
    public void SequentialReads_HandshakeLayout_ReadsAllFields()
    {
        // Handshake: VarInt(protoVersion), string(serverAddress), ushort(port), VarInt(nextState)
        // protoVersion=774 → 0x86 0x06
        // "localhost" → VarInt(9) + 9 байт
        // port=25565 → 0x63 0xDD (big-endian)
        // nextState=1 → 0x01
        byte[] bytes = {
            0x86, 0x06,                                                                    // VarInt(774)
            0x09, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'h', (byte)'o', (byte)'s', (byte)'t',
            0x63, 0xDD,                                                                    // ushort(25565) big-endian
            0x01                                                                           // VarInt(1)
        };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        bool v1 = reader.TryReadVarInt(out int protoVersion);
        bool v2 = reader.TryReadString(out string? address);
        bool v3 = reader.TryReadUShortBigEndian(out ushort port);
        bool v4 = reader.TryReadVarInt(out int nextState);

        Assert.True(v1 && v2 && v3 && v4);
        Assert.Equal(774, protoVersion);
        Assert.Equal("localhost", address);
        Assert.Equal((ushort)25565, port);
        Assert.Equal(1, nextState);
    }

    [Fact]
    public void SequentialReads_PartialMidway_AbortsGracefully()
    {
        // Урезанный Handshake: VarInt + string корректны, ushort обрезан.
        byte[] bytes = {
            0x86, 0x06,                    // VarInt(774)
            0x02, (byte)'a', (byte)'b',    // "ab"
            0x63                           // ushort обрезан (1 байт вместо 2)
        };
        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(bytes));

        Assert.True(reader.TryReadVarInt(out _));
        Assert.True(reader.TryReadString(out _));
        Assert.False(reader.TryReadUShortBigEndian(out _));
    }
}