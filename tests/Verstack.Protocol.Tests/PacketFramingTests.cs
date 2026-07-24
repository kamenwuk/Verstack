using System.Buffers;

namespace Verstack.Protocol.Tests;

/// <summary>
/// Tests for PacketFraming: frame writing via IBufferWriter,
/// round-trips with PacketFrameScanner, empty payload, multi-byte
/// length prefixes, multi-frame writes. Driven through
/// ArrayBufferWriter&lt;byte&gt; (no socket, no pipe).
/// </summary>
public class PacketFramingTests
{
    // ─── Wire bytes (формат кадра на проводе) ──────────────────────

    [Fact]
    public void Write_SingleByteLength_ProducesVarIntThenPayload()
    {
        // payload [1,2,3], длина 3 укладывается в 1-байтовый VarInt.
        var writer = new ArrayBufferWriter<byte>();

        PacketFraming.Write(writer, new byte[] { 1, 2, 3 });

        Assert.True(writer.WrittenSpan.SequenceEqual(new byte[] { 0x03, 1, 2, 3 }));
    }

    [Fact]
    public void Write_EmptyPayload_ProducesSingleZeroLengthByte()
    {
        // Пустой payload → кадр [0x00]. Главный кейс Status Request'а от клиента.
        var writer = new ArrayBufferWriter<byte>();

        PacketFraming.Write(writer, ReadOnlySpan<byte>.Empty);

        Assert.True(writer.WrittenSpan.SequenceEqual(new byte[] { 0x00 }));
    }

    [Fact]
    public void Write_TwoByteLength_ProducesMultiByteVarIntPrefix()
    {
        // 128 байт payload: VarInt(128) = [0x80, 0x01], затем 128 байт.
        var payload = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        var writer = new ArrayBufferWriter<byte>();

        PacketFraming.Write(writer, payload);

        // 2 байта VarInt + 128 байт payload = 130.
        Assert.Equal(130, writer.WrittenCount);
        Assert.Equal(0x80, writer.WrittenSpan[0]);
        Assert.Equal(0x01, writer.WrittenSpan[1]);
        Assert.True(writer.WrittenSpan[2..].SequenceEqual(payload));
    }

    // ─── Round-trip через scanner (главный кейс) ────────────────────

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x10, 0x00, 0xFF })]
    public void Write_ThenScan_ReturnsOriginalPayload(byte[] payload)
    {
        // Доказывает, что writer и scanner — честные зеркала:
        // то, что Framing записал, Scanner должен прочитать без потерь.
        var writer = new ArrayBufferWriter<byte>();
        PacketFraming.Write(writer, payload);

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(payload));
        Assert.False(scanner.MoveNext());
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }

    [Fact]
    public void Write_TwoByteLengthPayload_ThenScan_ReturnsOriginalPayload()
    {
        // 300 байт: VarInt длины будет мультибайтовым (0xAC 0x02).
        // Round-trip для нетривиального length-prefix.
        var payload = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        var writer = new ArrayBufferWriter<byte>();
        PacketFraming.Write(writer, payload);

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(payload));
    }

    // ─── Multi-frame запись ─────────────────────────────────────────

    [Fact]
    public void Write_TwoFrames_ThenScan_ReturnsBothInOrder()
    {
        // Два кадра подряд в один writer — scanner должен разобрать оба
        // в порядке записи. Проверяет, что Advance корректно двигает курсор.
        var writer = new ArrayBufferWriter<byte>();
        PacketFraming.Write(writer, new byte[] { 0xAA, 0xBB });
        PacketFraming.Write(writer, new byte[] { 0xCC, 0xDD, 0xEE });

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 0xAA, 0xBB }));
        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 0xCC, 0xDD, 0xEE }));
        Assert.False(scanner.MoveNext());
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }

    // ─── Atomic-запись (контракт IBufferWriter) ────────────────────

    [Fact]
    public void Write_SmallBuffer_KeepsFrameContiguous()
    {
        // ArrayBufferWriter с емкостью 1: каждый GetSpan(sizeHint) вернёт
        // блок, реаллоцированный до размера hint. Тест документирует, что
        // PacketFraming кладёт весь кадр в один Advance — на чтении сканер
        // не склеивает length-prefix с payload по сегментам.
        var writer = new ArrayBufferWriter<byte>(initialCapacity: 1);
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        PacketFraming.Write(writer, payload);

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(payload));
    }

#if DEBUG
    // ─── Защитная проверка (только Debug) ───────────────────────────

    [Fact]
    public void Write_PayloadExceedsMaxSize_Throws()
    {
        // В дебаге writer валидирует размер: превышение лимита = баг
        // в нашем сериализаторе. В релизе проверка снимается — горячий путь.
        var oversized = new byte[PacketFraming.DEFAULT_MAX_PACKET_SIZE + 1];
        var writer = new ArrayBufferWriter<byte>();

        Assert.Throws<ArgumentException>(
            () => PacketFraming.Write(writer, oversized));
    }
#endif
}