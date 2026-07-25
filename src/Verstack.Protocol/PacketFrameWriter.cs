using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Writes VarInt-length-prefixed frames to an <see cref="IBufferWriter{T}"/>.
/// Supports optional compression format.
/// </summary>
public static class PacketFrameWriter
{
    /// <summary>Default maximum packet size (2 MB).</summary>
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    /// <summary>
    /// Wraps <paramref name="payload"/> with a VarInt length prefix and writes the frame to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The buffer to write to (e.g. a <c>PipeWriter</c>).</param>
    /// <param name="payload">The packet body.</param>
    /// <param name="compressor">Compressor instance. If null, compression is disabled.</param>
    /// <param name="compressionThreshold">Minimum payload size to compress.</param>
    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload, 
        IPacketCompressor? compressor = null, int compressionThreshold = -1)
    {
        if (payload.Length > DEFAULT_MAX_PACKET_SIZE)
            throw new ArgumentException(
                $"Payload exceeds maximum packet size ({payload.Length} > {DEFAULT_MAX_PACKET_SIZE}).",
                nameof(payload));

        // Без сжатия
        if (compressor == null || compressionThreshold < 0)
        {
            int lengthBytes = VarInt.GetByteCount(payload.Length);
            Span<byte> span = output.GetSpan(lengthBytes + payload.Length);
            int written = VarInt.Encode(payload.Length, span);
            payload.CopyTo(span[written..]);
            output.Advance(written + payload.Length);
            return;
        }

        // Сжатие включено, но пакет слишком мал (DataLength = 0)
        if (payload.Length < compressionThreshold)
        {
            // Формат: [VarInt(packetLength)][VarInt(0)][payload]
            int packetLength = 1 + payload.Length; // 1 байт на VarInt(0)
            int lengthBytes = VarInt.GetByteCount(packetLength);
            
            Span<byte> span = output.GetSpan(lengthBytes + packetLength);
            int offset = VarInt.Encode(packetLength, span);
            offset += VarInt.Encode(0, span[offset..]); // DataLength = 0
            payload.CopyTo(span[offset..]);
            output.Advance(offset + payload.Length);
            return;
        }

        // Сжатие включено и пакет превышает threshold (DataLength > 0)
        // Обёрнуто в блок для изоляции области видимости переменных
        {
            int dataLength = payload.Length;
            int dataLengthVarIntSize = VarInt.GetByteCount(dataLength);
            
            // Запрашиваем у компрессора максимальный размер сжатых данных
            int maxCompressedSize = compressor.GetMaxCompressedSize(payload.Length);
            
            // Запрашиваем у PipeWriter слитный кусок памяти под весь кадр.
            // Резервируем VarInt.MAX_SIZE (5 байт) под длину пакета (packetLength),
            // так как до сжатия мы не знаем точный размер и количество байт VarInt.
            int totalFrameSize = VarInt.MAX_SIZE + dataLengthVarIntSize + maxCompressedSize;
            Span<byte> frameSpan = output.GetSpan(totalFrameSize);

            int offset = 0;
            
            // Пропускаем 5 байт под VarInt(packetLength), запишем его позже
            offset += VarInt.MAX_SIZE; 
            
            // Пишем DataLength (исходная длина payload)
            offset += VarInt.Encode(dataLength, frameSpan[offset..]);
            
            // Сжимаем данные прямо в выделенный span
            int compressedLength = compressor.Compress(payload, frameSpan[offset..]);
            offset += compressedLength;

            // Вычисляем фактическую длину пакета (без учёта самого VarInt(packetLength))
            int packetLength = offset - VarInt.MAX_SIZE;
            int actualPacketLengthVarIntSize = VarInt.GetByteCount(packetLength);
            
            // Так как фактический VarInt(packetLength) скорее всего короче 5 байт,
            // нам нужно сдвинуть DataLength и сжатые данные влево, чтобы убрать пустоты.
            int shift = VarInt.MAX_SIZE - actualPacketLengthVarIntSize;
            
            if (shift > 0)
            {
                // Копируем блок данных (DataLength + сжатые данные) влево.
                // MemoryExtensions.CopyTo корректно обрабатывает перекрывающиеся切片.
                frameSpan.Slice(VarInt.MAX_SIZE, packetLength).CopyTo(frameSpan.Slice(actualPacketLengthVarIntSize, packetLength));
            }

            // Пишем фактический VarInt(packetLength) в начало кадра
            VarInt.Encode(packetLength, frameSpan);

            // Подтверждаем запись ровно actualPacketLengthVarIntSize + packetLength байт
            output.Advance(actualPacketLengthVarIntSize + packetLength);
        }
    }
}