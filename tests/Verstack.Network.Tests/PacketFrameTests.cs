using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using Verstack.Network.Packet;
using System.IO.Compression;
using System.Buffers;

namespace Verstack.Network.Tests
{
    public class TestCompressor : IPacketCompressor
    {
        public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            using var ms = new MemoryStream();
            using var zs = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true);
            zs.Write(source);
            zs.Flush();
            
            byte[] compressed = ms.ToArray();
            compressed.CopyTo(destination);
            return compressed.Length;
        }

        public int GetMaxCompressedSize(int size) => size + 64;
    }

    public class TestDecompressor : IPacketDecompressor
    {
        public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
        {
            byte[] src = source.ToArray();
            using var ms = new MemoryStream(src);
            using var zs = new ZLibStream(ms, CompressionMode.Decompress);
            
            int totalRead = 0;
            while (totalRead < destination.Length)
            {
                int read = zs.Read(destination.Slice(totalRead));
                if (read == 0) break;
                totalRead += read;
            }
        }
    }

    public class PacketFrameTests
    {
        private readonly IPacketCompressor _compressor = new TestCompressor();
        private readonly IPacketDecompressor _decompressor = new TestDecompressor();

        private byte[] BuildPayload(int packetId, byte[] data)
        {
            var writer = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(64));
            writer.WriteVarInt(packetId).WriteSpan(data);
            return writer.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Проверяет полный цикл записи и чтения кадра при отключенном сжатии.
        /// Сценарий: Формируется пакет, пишется в буфер через PacketFrame.Write, затем читается через PacketFrame.TryRead.
        /// Ожидание: TryRead должен вернуть Complete, идентификатор пакета и данные должны совпасть с исходными,
        /// а указатель consumed должен указывать на конец буфера (весь буфер обработан).
        /// </summary>
        [Fact]
        public void WriteAndRead_Uncompressed_Roundtrip()
        {
            int threshold = -1;
            int packetId = 0x05;
            byte[] payloadData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            byte[] payload = BuildPayload(packetId, payloadData);

            var frameWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(128));
            PacketFrame.Write(ref frameWriter, payload, null, threshold);
            var sequence = new ReadOnlySequence<byte>(frameWriter.WrittenSpan.ToArray());

            var result = PacketFrame.TryRead(sequence, threshold, _decompressor, out int readId, out byte[] readData, out var consumed);

            Assert.Equal(PacketFrameResult.Complete, result);
            Assert.Equal(packetId, readId);
            Assert.Equal(payloadData, readData);
            Assert.Equal(sequence.Length, sequence.GetOffset(consumed));
        }

        /// <summary>
        /// Проверяет полный цикл записи и чтения кадра, размер которого превышает порог сжатия.
        /// Сценарий: Пакет (50 байт) пишется с порогом 10 байт. Данные должны быть сжаты через ZLib.
        /// Ожидание: TryRead должен успешно декомпрессировать данные, вернуть Complete, а исходные байты должны совпасть.
        /// </summary>
        [Fact]
        public void WriteAndRead_CompressedAboveThreshold_Roundtrip()
        {
            int threshold = 10;
            int packetId = 0x0A;
            byte[] payloadData = new byte[50];
            new Random(42).NextBytes(payloadData);
            byte[] payload = BuildPayload(packetId, payloadData);

            var frameWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(128));
            PacketFrame.Write(ref frameWriter, payload, _compressor, threshold);
            var sequence = new ReadOnlySequence<byte>(frameWriter.WrittenSpan.ToArray());

            var result = PacketFrame.TryRead(sequence, threshold, _decompressor, out int readId, out byte[] readData, out _);

            Assert.Equal(PacketFrameResult.Complete, result);
            Assert.Equal(packetId, readId);
            Assert.Equal(payloadData, readData);
        }

        /// <summary>
        /// Проверяет полный цикл записи и чтения кадра, размер которого меньше порога сжатия.
        /// Сценарий: Пакет (2 байта) пишется с порогом 100 байт. По протоколу Minecraft данные не сжимаются, 
        /// но кадр обрамляется заголовком со значением DataLength = 0.
        /// Ожидание: TryRead должен правильно разобрать этот спец-кадр, вернуть Complete и корректные данные без декомпрессии.
        /// </summary>
        [Fact]
        public void WriteAndRead_CompressedBelowThreshold_Roundtrip()
        {
            int threshold = 100;
            int packetId = 0x0B;
            byte[] payloadData = new byte[] { 0xAA, 0xBB };
            byte[] payload = BuildPayload(packetId, payloadData);

            var frameWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(128));
            PacketFrame.Write(ref frameWriter, payload, _compressor, threshold);
            var sequence = new ReadOnlySequence<byte>(frameWriter.WrittenSpan.ToArray());

            var result = PacketFrame.TryRead(sequence, threshold, _decompressor, out int readId, out byte[] readData, out _);

            Assert.Equal(PacketFrameResult.Complete, result);
            Assert.Equal(packetId, readId);
            Assert.Equal(payloadData, readData);
        }

        /// <summary>
        /// Проверяет поведение фрейминга при получении неполного кадра (обрезанного пакета).
        /// Сценарий: Из готового кадра удаляется последний байт, и этот укороченный буфер передается в TryRead.
        /// Ожидание: Метод должен вернуть Partial, сигнализируя о том, что данных недостаточно и нужно ждать продолжения.
        /// </summary>
        [Fact]
        public void TryRead_PartialBuffer_ReturnsPartial()
        {
            byte[] payload = BuildPayload(1, new byte[] { 0x01 });
            var frameWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(128));
            PacketFrame.Write(ref frameWriter, payload, null, -1);

            var fullSequence = new ReadOnlySequence<byte>(frameWriter.WrittenSpan.ToArray());
            var partialSequence = fullSequence.Slice(0, fullSequence.Length - 1);

            var result = PacketFrame.TryRead(partialSequence, -1, _decompressor, out _, out _, out _);

            Assert.Equal(PacketFrameResult.Partial, result);
        }

        /// <summary>
        /// Проверяет обработку некорректного кадра с нулевой длиной пакета.
        /// Сценарий: В буфере находится один байт 0x00 (VarInt, означающий длину пакета = 0).
        /// Ожидание: TryRead должен мгновенно вернуть Malformed, так как нулевая длина пакета нарушает протокол Minecraft.
        /// </summary>
        [Fact]
        public void TryRead_MalformedZeroLength_ReturnsMalformed()
        {
            byte[] data = { 0x00 };
            var sequence = new ReadOnlySequence<byte>(data);

            var result = PacketFrame.TryRead(sequence, -1, _decompressor, out _, out _, out _);

            Assert.Equal(PacketFrameResult.Malformed, result);
        }

        /// <summary>
        /// Проверяет обработку кадра с поврежденным ZLib-потоком.
        /// Сценарий: Формируется корректный сжатый кадр, затем в середине сжатых данных перезаписываются 2 байта.
        /// Ожидание: TryRead должен перехватить исключение от декомпрессора и вернуть Malformed, 
        /// так как дальнейший разбор потока невозможен.
        /// </summary>
        [Fact]
        public void TryRead_MalformedZlib_ReturnsMalformed()
        {
            int threshold = 10;
            int packetId = 0x0C;
            byte[] payloadData = new byte[20];
            byte[] payload = BuildPayload(packetId, payloadData);

            var frameWriter = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(128));
            PacketFrame.Write(ref frameWriter, payload, _compressor, threshold);

            byte[] frameBytes = frameWriter.WrittenSpan.ToArray();
            
            // Портим сжатые данные в середине кадра, чтобы сломать deflate-блок,
            // а не контрольную сумму в конце (которую ZLibStream может не успеть проверить)
            int mid = frameBytes.Length / 2;
            frameBytes[mid] = 0xFF;
            frameBytes[mid + 1] = 0xFF;

            var sequence = new ReadOnlySequence<byte>(frameBytes);

            var result = PacketFrame.TryRead(sequence, threshold, _decompressor, out _, out _, out _);

            Assert.Equal(PacketFrameResult.Malformed, result);
        }
    }
}