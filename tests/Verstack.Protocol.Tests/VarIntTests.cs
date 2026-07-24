using System.Buffers;
using Verstack.Protocol;

namespace Verstack.Protocol.Tests;

/// <summary>
/// VarInt tests: Encode/TryDecode on Span, TryRead on SequenceReader,
/// round-trips, edge values, partial/corrupted data, multi-segment reads.
/// Driven through Span/Sequence without a socket.
/// </summary>
public class VarIntTests
{
    // ─── Encode (Span) ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, new byte[] { 0x00 })]
    [InlineData(1, new byte[] { 0x01 })]
    [InlineData(127, new byte[] { 0x7F })]
    [InlineData(128, new byte[] { 0x80, 0x01 })]
    [InlineData(255, new byte[] { 0xFF, 0x01 })]
    [InlineData(300, new byte[] { 0xAC, 0x02 })]
    [InlineData(16384, new byte[] { 0x80, 0x80, 0x01 })]
    public void Encode_KnownValues_ProducesExpectedBytes(int value, byte[] expected)
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];

        int written = VarInt.Encode(value, buf);

        Assert.Equal(expected.Length, written);
        Assert.True(buf[..written].SequenceEqual(expected));
    }

    // ─── TryDecode (Span) ────────────────────────────────────────────

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0)]
    [InlineData(new byte[] { 0x01 }, 1)]
    [InlineData(new byte[] { 0x7F }, 127)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128)]
    [InlineData(new byte[] { 0xAC, 0x02 }, 300)]
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16384)]
    public void TryDecode_KnownBytes_ProducesExpectedValue(byte[] bytes, int expected)
    {
        bool ok = VarInt.TryDecode(bytes, out int value, out int consumed);

        Assert.True(ok);
        Assert.Equal(expected, value);
        Assert.Equal(bytes.Length, consumed);
    }

    [Theory]
    [InlineData(new byte[] { 0xAC })]            // continuation есть, 2-го байта нет
    [InlineData(new byte[] { 0x80, 0x80 })]       // continuation есть, 3-го байта нет
    public void TryDecode_PartialData_ReturnsFalse(byte[] bytes)
    {
        bool ok = VarInt.TryDecode(bytes, out int value, out int consumed);

        Assert.False(ok);
        Assert.Equal(0, value);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void TryDecode_OverflowMoreThan5Bytes_ReturnsFalse()
    {
        // 6 байт continuation: невозможный VarInt, данные битые.
        byte[] bytes = { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 };

        bool ok = VarInt.TryDecode(bytes, out int value, out int consumed);

        Assert.False(ok);
        Assert.Equal(0, value);
        Assert.Equal(0, consumed);
    }

    // ─── TryRead (SequenceReader) ───────────────────────────────────

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0)]
    [InlineData(new byte[] { 0x7F }, 127)]
    [InlineData(new byte[] { 0x80, 0x01 }, 128)]
    [InlineData(new byte[] { 0xAC, 0x02 }, 300)]
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16384)]
    public void TryRead_Reader_Complete_ReturnsExpectedValue(byte[] bytes, int expected)
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        VarInt.ReadStatus status = VarInt.TryRead(ref reader, out int value);

        Assert.Equal(VarInt.ReadStatus.Complete, status);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryRead_Reader_Partial_ReturnsPartialAndZeroValue()
    {
        // continuation есть, второго байта нет → частичные данные.
        byte[] bytes = { 0xAC };
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        VarInt.ReadStatus status = VarInt.TryRead(ref reader, out int value);

        Assert.Equal(VarInt.ReadStatus.Partial, status);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryRead_Reader_Malformed_ReturnsMalformedAndZeroValue()
    {
        // 5 байт continuation: VarInt не закрылся за MAX_SIZE → битый.
        byte[] bytes = { 0x80, 0x80, 0x80, 0x80, 0x80 };
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        VarInt.ReadStatus status = VarInt.TryRead(ref reader, out int value);

        Assert.Equal(VarInt.ReadStatus.Malformed, status);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryRead_Reader_AdvancesPastConsumedBytes()
    {
        // VarInt(300) занимает 2 байта; третий байт должен остаться непрочитанным.
        byte[] bytes = { 0xAC, 0x02, 0xFF };
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

        VarInt.TryRead(ref reader, out _);

        Assert.Equal(2, reader.Consumed);
    }

    [Fact]
    public void TryRead_Reader_SplitAcrossSegments_ReadsAcrossBoundary()
    {
        // VarInt(300) = [0xAC, 0x02], разрезан между двумя сегментами —
        // главный кейс: SequenceReader должен дочитать continuation из 2-го сегмента.
        var sequence = TestSequenceBuilder.BuildSegmented(
            new byte[] { 0xAC },
            new byte[] { 0x02 });

        var reader = new SequenceReader<byte>(sequence);

        VarInt.ReadStatus status = VarInt.TryRead(ref reader, out int value);

        Assert.Equal(VarInt.ReadStatus.Complete, status);
        Assert.Equal(300, value);
    }

    // ─── Round-trip ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(300)]
    [InlineData(16384)]
    [InlineData(int.MaxValue)]
    public void RoundTrip_EncodeThenDecode_ReturnsOriginal(int value)
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];

        int written = VarInt.Encode(value, buf);
        bool ok = VarInt.TryDecode(buf[..written], out int decoded, out int consumed);

        Assert.True(ok);
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void RoundTrip_NegativeAndExtremeValues_ReturnsOriginal(int value)
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];

        int written = VarInt.Encode(value, buf);
        bool ok = VarInt.TryDecode(buf[..written], out int decoded, out int consumed);

        Assert.True(ok);
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(300)]
    [InlineData(int.MaxValue)]
    public void RoundTrip_EncodeThenTryReadReader_ReturnsOriginal(int value)
    {
        Span<byte> buf = stackalloc byte[VarInt.MAX_SIZE];
        int written = VarInt.Encode(value, buf);

        var reader = new SequenceReader<byte>(
            new ReadOnlySequence<byte>(buf[..written].ToArray()));

        VarInt.ReadStatus status = VarInt.TryRead(ref reader, out int decoded);

        Assert.Equal(VarInt.ReadStatus.Complete, status);
        Assert.Equal(value, decoded);
    }
}