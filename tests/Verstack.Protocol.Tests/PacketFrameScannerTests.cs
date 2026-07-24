using System.Buffers;
using Verstack.Protocol;

namespace Verstack.Protocol.Tests;

/// <summary>
/// Tests for PacketFrameScanner: complete frames, partial/corrupted data,
/// multi-segment sequences, ConsumedPosition contract.
/// </summary>
public class PacketFrameScannerTests
{
    [Fact]
    public void MoveNext_SingleCompleteFrame_ReturnsTrueAndPayload()
    {
        // [VarInt(5)][payload: 5 байт]
        byte[] input = [0x05, 1, 2, 3, 4, 5];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input));

        bool moved = scanner.MoveNext();

        Assert.True(moved);
        Assert.Equal(VarInt.ReadStatus.Complete, scanner.Status);
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Fact]
    public void MoveNext_MultipleFrames_ReturnsEachInOrder()
    {
        // [frame len=2][AA BB][frame len=3][CC DD EE]
        byte[] input = [0x02, 0xAA, 0xBB, 0x03, 0xCC, 0xDD, 0xEE];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input));

        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 0xAA, 0xBB }));
        Assert.True(scanner.MoveNext());
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 0xCC, 0xDD, 0xEE }));
        Assert.False(scanner.MoveNext());
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }

    [Fact]
    public void MoveNext_PartialVarInt_ReturnsPartial()
    {
        // continuation есть, второго байта нет.
        byte[] input = [0xAC];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input));

        bool moved = scanner.MoveNext();

        Assert.False(moved);
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }

    [Fact]
    public void MoveNext_PartialPayload_ReturnsPartial()
    {
        // VarInt заявляет 5 байт, payload только 3.
        byte[] input = [0x05, 1, 2, 3];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input));

        bool moved = scanner.MoveNext();

        Assert.False(moved);
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }

    [Fact]
    public void MoveNext_OversizedFrame_ReturnsMalformed()
    {
        // VarInt(2) + 1 байт payload, но maxPacketSize=1 → oversized.
        byte[] input = [0x02, 0xFF];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input), maxPacketSize: 1);

        bool moved = scanner.MoveNext();

        Assert.False(moved);
        Assert.Equal(VarInt.ReadStatus.Malformed, scanner.Status);
    }

    [Fact]
    public void MoveNext_CorruptedVarInt_ReturnsMalformed()
    {
        // 5 байт continuation: VarInt не закрылся → битый.
        byte[] input = [0x80, 0x80, 0x80, 0x80, 0x80];
        var scanner = new PacketFrameScanner(new ReadOnlySequence<byte>(input));

        bool moved = scanner.MoveNext();

        Assert.False(moved);
        Assert.Equal(VarInt.ReadStatus.Malformed, scanner.Status);
    }

    [Fact]
    public void ConsumedPosition_PartialPointsToFrameStart()
    {
        // [целый кадр len=1][частичный: VarInt=5, payload 3 байта]
        byte[] input = [0x01, 0xFF, 0x05, 1, 2, 3];
        var sequence = new ReadOnlySequence<byte>(input);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());   // первый кадр целый
        Assert.False(scanner.MoveNext());  // второй — Partial
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);

        // ConsumedPosition указывает на начало частичного кадра (0x05 на позиции 2),
        // а не на текущую позицию SequenceReader (после чтения VarInt).
        long consumedOffset = sequence.GetOffset(scanner.ConsumedPosition);
        Assert.Equal(2, consumedOffset);
    }

    [Fact]
    public void ConsumedPosition_CompletePointsToReaderPosition()
    {
        // Один целый кадр потребляет весь буфер.
        byte[] input = [0x02, 0xAA, 0xBB];
        var sequence = new ReadOnlySequence<byte>(input);
        var scanner = new PacketFrameScanner(sequence);

        Assert.True(scanner.MoveNext());

        long consumedOffset = sequence.GetOffset(scanner.ConsumedPosition);
        Assert.Equal(3, consumedOffset);
    }

    [Fact]
    public void Foreach_YieldsAllCompleteFrames()
    {
        byte[] input = [0x02, 0xAA, 0xBB, 0x02, 0xCC, 0xDD];
        var sequence = new ReadOnlySequence<byte>(input);

        var payloads = new List<byte[]>();
        foreach (var payload in new PacketFrameScanner(sequence))
        {
            payloads.Add(payload.ToArray());
        }

        Assert.Equal(2, payloads.Count);
        Assert.True(payloads[0].SequenceEqual(new byte[] { 0xAA, 0xBB }));
        Assert.True(payloads[1].SequenceEqual(new byte[] { 0xCC, 0xDD }));
    }

    [Fact]
    public void MoveNext_MultiSegment_SplitVarInt_ReadsAcrossBoundary()
    {
        // VarInt(128)=[0x80,0x01] разрезан между сегментами; payload=128 байт.
        // length=128 выбран, чтобы VarInt был мультибайтовым (2 байта) —
        // только так его можно разрезать между сегментами. Payload задаётся
        // той же длиной, иначе сканер честно сообщит Partial.
        byte[] payload = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        var sequence = TestSequenceBuilder.BuildSegmented(
            [0x80],
            new byte[] { 0x01 }.Concat(payload).ToArray());
        var scanner = new PacketFrameScanner(sequence);

        bool moved = scanner.MoveNext();

        Assert.True(moved);
        Assert.Equal(VarInt.ReadStatus.Complete, scanner.Status);
        Assert.True(scanner.Current.ToArray().SequenceEqual(payload));
    }

    [Fact]
    public void MoveNext_MultiSegment_SplitPayload_ReadsAcrossBoundary()
    {
        // payload [1,2,3,4] разрезан: seg1=[VarInt(4), 1, 2], seg2=[3, 4]
        var sequence = TestSequenceBuilder.BuildSegmented(
            [0x04, 1, 2],
            [3, 4]);
        var scanner = new PacketFrameScanner(sequence);

        bool moved = scanner.MoveNext();

        Assert.True(moved);
        Assert.True(scanner.Current.ToArray().SequenceEqual(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void MoveNext_EmptyInput_ReturnsPartial()
    {
        var scanner = new PacketFrameScanner(ReadOnlySequence<byte>.Empty);

        bool moved = scanner.MoveNext();

        Assert.False(moved);
        Assert.Equal(VarInt.ReadStatus.Partial, scanner.Status);
    }
}