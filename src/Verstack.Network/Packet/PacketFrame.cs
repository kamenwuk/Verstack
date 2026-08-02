using Verstack.Network.Packet.Writers;
using System.Runtime.CompilerServices;
using Verstack.Network.Compression;
using System.Buffers;

namespace Verstack.Network.Packet;

/// <summary>
/// Обеспечивает фрейминг пакетов протокола: разбор и упаковку кадров с учётом порога сжатия.
/// </summary>
/// <remarks>
/// Форматы кадров:
/// <list type="bullet">
///   <item>Несжатый: <c>[VarInt(PacketLength)][VarInt(PacketId) + data]</c>.</item>
///   <item>Сжатый (compressionThreshold ≥ 0): <c>[VarInt(PacketLength)][VarInt(DataLength) + payload]</c>,
///     где payload — либо несжатый (DataLength=0, если размер меньше threshold), либо zlib-сжатый (DataLength=N).</item>
/// </list>
/// Контракт исходящих данных одинаковый для обоих режимов: на вход подаётся <c>[VarInt(PacketId) + data]</c>,
/// а слой фрейминга сам заворачивает его в правильный кадр.
/// </remarks>
public static class PacketFrame
{
    /// <summary>
    /// Разбирает один кадр из буфера с учётом порога сжатия.
    /// </summary>
    /// <param name="buffer">Входной буфер данных.</param>
    /// <param name="compressionThreshold">Порог сжатия. Значение меньше 0 отключает сжатие.</param>
    /// <param name="decompressor">Декомпрессор для распаковки zlib-потока.</param>
    /// <param name="packetId">Извлечённый идентификатор пакета.</param>
    /// <param name="data">Извлечённая полезная нагрузка пакета (без идентификатора).</param>
    /// <param name="consumed">Позиция, до которой буфер можно сдвинуть после успешного чтения.</param>
    /// <returns>Результат попытки разобрать кадр. 
    /// При <c>Complete</c> возвращает пакет и сдвиг. 
    /// При <c>Partial</c> или <c>Malformed</c> буфер нельзя трогать.</returns>
    public static PacketFrameResult TryRead(ReadOnlySequence<byte> buffer, int compressionThreshold,
        IPacketDecompressor decompressor, out int packetId, out byte[] data, out SequencePosition consumed)
    {
        packetId = 0;
        data = null;
        consumed = buffer.Start;

        var reader = new SequenceReader<byte>(buffer);

        // 1. Длина пакета (VarInt)
        if (!TryReadVarInt(ref reader, out int length))
            return PacketFrameResult.Partial;

        if (length <= 0)
            return PacketFrameResult.Malformed;

        // 2. Тело пакета должно быть целиком в буфере.
        if (reader.Remaining < length)
            return PacketFrameResult.Partial;

        var bodyStart = reader.Position;
        consumed = buffer.GetPosition(length, bodyStart);

        // --- Несжатый framing ---
        if (compressionThreshold < 0)
        {
            return ReadUncompressed(buffer, bodyStart, length, out packetId, out data)
                ? PacketFrameResult.Complete
                : PacketFrameResult.Malformed;
        }

        // --- Compressed framing ---
        var dataLenReader = new SequenceReader<byte>(buffer.Slice(bodyStart, length));
        if (!TryReadVarInt(ref dataLenReader, out var dataLength))
            return PacketFrameResult.Malformed;

        int payloadLength = length - (int)dataLenReader.Consumed;
        var payloadStart = dataLenReader.Position;

        if (dataLength == 0)
        {
            var payload = buffer.Slice(bodyStart).Slice(payloadStart);
            return ReadUncompressed(payload, payload.Start, payloadLength, out packetId, out data)
                ? PacketFrameResult.Complete
                : PacketFrameResult.Malformed;
        }

        // Пакет сжат
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
        if (!TryReadVarInt(ref inner, out packetId))
            return PacketFrameResult.Malformed;

        int consumedInner = (int)inner.Consumed;
        int dataLen = dataLength - consumedInner;
        data = new byte[dataLen];
        decompressed.AsSpan(consumedInner, dataLen).CopyTo(data);
        
        return PacketFrameResult.Complete;
    }

    /// <summary>
    /// Разбирает несжатое тело пакета формата <c>[VarInt(PacketId)][data]</c>.
    /// </summary>
    /// <param name="buffer">Входной буфер данных.</param>
    /// <param name="bodyStart">Начальная позиция тела пакета в буфере.</param>
    /// <param name="length">Общий размер тела в байтах.</param>
    /// <param name="packetId">Извлечённый идентификатор пакета.</param>
    /// <param name="data">Извлечённая полезная нагрузка.</param>
    /// <returns><c>true</c>, если разбор прошёл успешно; иначе <c>false</c>.</returns>
    private static bool ReadUncompressed(ReadOnlySequence<byte> buffer, SequencePosition bodyStart, int length,
        out int packetId, out byte[] data)
    {
        packetId = 0;
        data = null;

        var body = buffer.Slice(bodyStart, length);
        var reader = new SequenceReader<byte>(body);
        
        if (!TryReadVarInt(ref reader, out packetId))
            return false;

        int dataLen = length - (int)reader.Consumed;
        data = new byte[dataLen];
        body.Slice(reader.Position).CopyTo(data);
        return true;
    }

    /// <summary>
    /// Упаковывает готовую полезную нагрузку в кадр по порогу сжатия и записывает в поток.
    /// </summary>
    /// <param name="streamWriter">Писатель потока для записи кадра.</param>
    /// <param name="payload">Полезная нагрузка (должна содержать VarInt(PacketId) и данные).</param>
    /// <param name="compressor">Компрессор для сжатия данных (может быть null, если сжатие отключено).</param>
    /// <param name="compressionThreshold">Порог сжатия. Значение меньше 0 отключает сжатие.</param>
    /// <remarks>
    /// Метод не разбирает внутреннюю структуру <paramref name="payload"/> и работает с ним как с чёрным ящиком.
    /// </remarks>
    public static void Write(ref PacketStreamWriter streamWriter, ReadOnlySpan<byte> payload,
        IPacketCompressor compressor, int compressionThreshold)
    {
        int payloadSize = payload.Length;

        if (compressionThreshold < 0 || compressor == null)
        {
            streamWriter.WriteVarInt(payloadSize).WriteSpan(payload);
            return;
        }

        if (payloadSize < compressionThreshold)
        {
            int packetLength = 1 + payloadSize; // VarInt(0) всегда 1 байт
            streamWriter.WriteVarInt(packetLength).WriteVarInt(0).WriteSpan(payload);
            return;
        }

        int maxCompressed = compressor.GetMaxCompressedSize(payloadSize);
        byte[] compressed = ArrayPool<byte>.Shared.Rent(maxCompressed);
        try
        {
            int compressedLen = compressor.Compress(payload, compressed);
            int innerLen = GetVarIntSize(payloadSize) + compressedLen;
            
            streamWriter.WriteVarInt(innerLen)
                .WriteVarInt(payloadSize)
                .WriteSpan(compressed.AsSpan(0, compressedLen));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressed);
        }
    }

    /// <summary>
    /// Пытается прочитать 32-битное значение переменной длины (VarInt) из последовательности.
    /// </summary>
    /// <param name="reader">Читатель последовательности байт.</param>
    /// <param name="value">Прочитанное значение.</param>
    /// <returns><c>true</c>, если чтение успешно; <c>false</c> при нехватке данных или превышении лимита в 5 байт.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadVarInt(ref SequenceReader<byte> reader, out int value)
    {
        value = 0;
        int shift = 0;
        byte b;

        do
        {
            if (!reader.TryRead(out b))
                return false;

            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0 && shift < 35);

        if ((b & 0x80) != 0)
            return false;

        return true;
    }

    /// <summary>
    /// Вычисляет количество байт, необходимое для записи значения в формате VarInt.
    /// </summary>
    /// <param name="value">Значение для кодирования.</param>
    /// <returns>Размер в байтах (от 1 до 5).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVarIntSize(int value)
    {
        if ((value & (0xFFFFFFFF << 7)) == 0) return 1;
        if ((value & (0xFFFFFFFF << 14)) == 0) return 2;
        if ((value & (0xFFFFFFFF << 21)) == 0) return 3;
        if ((value & (0xFFFFFFFF << 28)) == 0) return 4;
        return 5;
    }
}