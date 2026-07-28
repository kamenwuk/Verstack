using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using System.Buffers;

namespace Verstack.Network.Packet;

/// <summary>
/// Фрейминг пакетов Minecraft: разбор и упаковка кадров с учётом порога сжатия.
///
/// Несжатый framing: <c>[VarInt(PacketLength)][VarInt(PacketId) + data]</c>.
/// Compressed framing (compressionThreshold ≥ 0): <c>[VarInt(PacketLength)][VarInt(DataLength) + payload]</c>,
/// где payload — либо несжатый (DataLength=0, пакет меньше threshold), либо zlib-сжатый (DataLength=N).
/// Контракт исходящих данных одинаковый для обоих режимов: <c>[VarInt(PacketId) + data]</c> —
/// framing-слой сам заворачивает в правильный кадр.
/// </summary>
public static class PacketFrame
{
    /// <summary>
    /// Разбирает один кадр из <paramref name="buffer"/> с учётом порога сжатия.
    /// При <see cref="PacketFrameResult.Complete"/> возвращает пакет и <paramref name="consumed"/> —
    /// позицию, до которой буфер можно сдвинуть. В остальных случаях буфер НЕ трогать.
    /// </summary>
    public static PacketFrameResult TryRead(ReadOnlySequence<byte> buffer, int compressionThreshold,
        IPacketDecompressor decompressor, out int packetId, out byte[] data, out SequencePosition consumed)
    {
        packetId = 0;
        data = null;
        consumed = buffer.Start;

        var reader = new SequenceReader<byte>(buffer);

        // 1. Длина пакета (VarInt) — общий префикс для обоих framing'ов.
        if (!VarInt.TryRead(ref reader, out int length))
            return PacketFrameResult.Partial;

        // Нулевая или отрицательная длина — гарантированно битый кадр.
        if (length <= 0)
            return PacketFrameResult.Malformed;

        // 2. Тело пакета должно быть целиком в буфере.
        if (reader.Remaining < length)
            return PacketFrameResult.Partial;

        // 3. Запоминаем границы тела: bodyStart..packetEnd.
        var bodyStart = reader.Position;
        consumed = buffer.GetPosition(length, bodyStart);

        // --- Несжатый framing ---
        if (compressionThreshold < 0)
        {
            return ReadUncompressed(buffer, bodyStart, length, out packetId, out data)
                ? PacketFrameResult.Complete
                : PacketFrameResult.Malformed;
        }

        // --- Compressed framing: тело = [VarInt(DataLength)][payload] ---
        var dataLenReader = new SequenceReader<byte>(buffer.Slice(bodyStart, length));
        if (!VarInt.TryRead(ref dataLenReader, out var dataLength))
            return PacketFrameResult.Malformed;

        // Consumed = сколько байт занял VarInt(DataLength) → остальное в кадре это payload.
        int payloadLength = length - (int)dataLenReader.Consumed;
        var payloadStart = dataLenReader.Position;

        if (dataLength == 0)
        {
            // Пакет меньше threshold — payload несжатый: [VarInt(PacketId)][data].
            var payload = buffer.Slice(bodyStart).Slice(payloadStart);
            return ReadUncompressed(payload, payload.Start, payloadLength, out packetId, out data)
                ? PacketFrameResult.Complete
                : PacketFrameResult.Malformed;
        }

        // Пакет сжат: декомпрессим payloadLength байт → dataLength байт = [VarInt(PacketId)][data].
#if DEBUG
        if (decompressor == null)
            throw new InvalidOperationException(
                $"[{nameof(PacketFrame)}] Сжатый кадр (DataLength={dataLength}), но декомпрессор не задан.");
#endif
        var compressed = buffer.Slice(bodyStart).Slice(payloadStart, payloadLength);
        byte[] decompressed = new byte[dataLength];
        try
        {
            decompressor.Decompress(compressed, decompressed);
        }
        catch
        {
            return PacketFrameResult.Malformed;
        }

        var inner = new SequenceReader<byte>(new ReadOnlySequence<byte>(decompressed));
        if (!VarInt.TryRead(ref inner, out packetId))
            return PacketFrameResult.Malformed;

        int consumedInner = (int)inner.Consumed;
        int dataLen = dataLength - consumedInner;
        data = new byte[dataLen];
        decompressed.AsSpan(consumedInner, dataLen).CopyTo(data);
        return PacketFrameResult.Complete;
    }

    /// <summary>
    /// Несжатое тело: <c>[VarInt(PacketId)][data]</c>. <paramref name="length"/> — размер всего тела.
    /// </summary>
    private static bool ReadUncompressed(ReadOnlySequence<byte> buffer, SequencePosition bodyStart, int length,
        out int packetId, out byte[] data)
    {
        packetId = 0;
        data = null;

        var body = buffer.Slice(bodyStart, length);
        var reader = new SequenceReader<byte>(body);
        if (!VarInt.TryRead(ref reader, out packetId))
            return false;

        int dataLen = length - (int)reader.Consumed;
        data = new byte[dataLen];
        body.Slice(reader.Position).CopyTo(data);
        return true;
    }

    /// <summary>
    /// Упаковывает готовый payload (с уже записанным внутри VarInt(PacketId)) в кадр
    /// по порогу сжатия и пишет в <paramref name="writer"/>. framing-слой не разбирает
    /// внутреннюю структуру payload — работает с ним как с чёрным ящиком.
    /// </summary>
    public static void Write(ref SpanWriter writer, ReadOnlySpan<byte> payload,
        IPacketCompressor compressor, int compressionThreshold)
    {
        int payloadSize = payload.Length;

        // --- Несжатый framing: [VarInt(payloadSize)][payload] ---
        if (compressionThreshold < 0 || compressor == null)
        {
            VarInt.Write(ref writer, payloadSize);
            payload.CopyTo(writer.GetSpan(payloadSize));
            writer.Advance(payloadSize);
            return;
        }

        // --- Compressed framing ---
        if (payloadSize < compressionThreshold)
        {
            // payload меньше threshold → DataLength=0, тело несжатое.
            int packetLength = 1 + payloadSize; // VarInt(0) всегда 1 байт
            VarInt.Write(ref writer, packetLength);
            VarInt.Write(ref writer, 0);
            payload.CopyTo(writer.GetSpan(payloadSize));
            writer.Advance(payloadSize);
            return;
        }

        // payload ≥ threshold → сжимаем. DataLength = payloadSize (исходная длина).
        int maxCompressed = compressor.GetMaxCompressedSize(payloadSize);
        byte[] compressed = ArrayPool<byte>.Shared.Rent(maxCompressed);
        try
        {
            int compressedLen = compressor.Compress(payload, compressed);

            int innerLen = VarInt.GetSize(payloadSize) + compressedLen;
            VarInt.Write(ref writer, innerLen);
            VarInt.Write(ref writer, payloadSize);
            compressed.AsSpan(0, compressedLen).CopyTo(writer.GetSpan(compressedLen));
            writer.Advance(compressedLen);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressed);
        }
    }
}