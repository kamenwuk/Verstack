using System.Buffers;

namespace Verstack.Network.Compression;

/// <summary>
/// Абстракция декомпрессии пакетов для framing-слоя. Реализация по умолчанию — zlib (RFC 1950),
/// см. <see cref="ZLibPacketDecompressor"/>.
/// </summary>
public interface IPacketDecompressor
{
    /// <summary>
    /// Декомпрессирует <paramref name="source"/> в <paramref name="destination"/>.
    /// Размер <paramref name="destination"/> равен исходной (до сжатия) длине — DataLength из кадра.
    /// </summary>
    void Decompress(ReadOnlySequence<byte> source, Span<byte> destination);
}