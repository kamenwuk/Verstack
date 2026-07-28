namespace Verstack.Network.Compression;

/// <summary>
/// Абстракция сжатия пакетов для framing-слоя. Реализация по умолчанию — zlib (RFC 1950),
/// см. <see cref="ZLibPacketCompressor"/>.
/// </summary>
public interface IPacketCompressor
{
    /// <summary>
    /// Максимальный размер сжатых данных для исходной длины — для резервирования буфера до сжатия.
    /// </summary>
    int GetMaxCompressedSize(int sourceLength);

    /// <summary>
    /// Сжимает <paramref name="source"/> в <paramref name="destination"/>. Буфер назначения должен быть
    /// не меньше <see cref="GetMaxCompressedSize"/>.
    /// </summary>
    /// <returns>Число записанных байт.</returns>
    int Compress(ReadOnlySpan<byte> source, Span<byte> destination);
}