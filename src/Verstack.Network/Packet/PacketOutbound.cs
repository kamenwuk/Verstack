using Verstack.Network.Compression;
using Verstack.Network.Packet.Writers;

namespace Verstack.Network.Packet;

/// <summary>
/// Выходной интерфейс пакета для бандлов: ref struct на стеке обработки одной сущности.
/// Скрывает framing и compression — бандл описывает пакет (ID + поля payload), всё остальное делает транспорт.
///
/// Два буфера (владелец — PacketDispatchSystem, арендует на весь Run через ArrayPool — heap):
/// <list type="bullet">
///   <item><see cref="PayloadBuffer"/> — временный heap-буфер под payload. Бандл берёт его, создаёт
///     <c>SpanWriter</c> поверх него, пишет поля, передаёт <see cref="SpanWriter.WrittenSpan"/>
///     в <see cref="Send"/>. Heap-буфер означает, что Span не привязан к стек-фрейму бандла —
///     передаётся по значению без scope-ограничений ref struct.</item>
///   <item><c>_frameScratch</c> — framing-выход, растёт вправо от 0, contiguous для отправки.</item>
/// </list>
///
/// <see cref="Send"/> читает <see cref="Network.NetworkChannel.CompressionThreshold"/> «вживую»:
/// смена framing'а (<see cref="EnableCompression"/>) применяется к последующим пакетам в том же вызове.
/// Это позволяет смешивать сжатые и несжатые пакеты — Set Compression уходит несжатым, Login Success — сжатым.
/// </summary>
public ref struct PacketOutbound(NetworkChannel channel, IPacketCompressor compressor,
    Span<byte> frameScratch, Span<byte> payloadBuffer)
{
    private readonly Span<byte> _frameScratch = frameScratch;
    private int _frameOffset = 0;

    /// <summary>
    /// Временный heap-бufer для сборки payload текущего пакета. Бандл создаёт <c>SpanWriter</c>
    /// поверх него, пишет поля, передаёт <c>WrittenSpan</c> в <see cref="Send"/>.
    /// </summary>
    public Span<byte> PayloadBuffer { get; } = payloadBuffer;

    /// <summary>
    /// Завершает пакет: упаковывает payload в кадр по текущему threshold канала и пишет framing в frameScratch.
    /// </summary>
    public void Send(ReadOnlySpan<byte> payload)
    {
        var frameWriter = new PacketWriter(_frameScratch[_frameOffset..]);
        PacketFrame.Write(ref frameWriter, payload, compressor, channel.CompressionThreshold);
        _frameOffset += frameWriter.Written;
    }

    /// <summary>
    /// Включает compression на канале: ставит threshold. Все последующие пакеты (в обе стороны)
    /// идут в compressed framing. Шлётся только ПОСЛЕ пакета Set Compression.
    /// </summary>
    public void EnableCompression(int threshold) => channel.CompressionThreshold = threshold;

    /// <summary>Сколько framing-байт накоплено в frameScratch.</summary>
    public int Written => _frameOffset;

    /// <summary>Готовые framing-байты для flush в канал (contiguous с начала frameScratch).</summary>
    public ReadOnlySpan<byte> WrittenSpan => _frameScratch[.._frameOffset];

    /// <summary>Обнуляет framing-offset — переиспользование на следующей сущности.</summary>
    public void Reset() => _frameOffset = 0;
    
    public void Flush()
    {
        if (Written > 0)
        {
            channel.EnqueueOutbound(WrittenSpan);
            Reset(); // Сброс курсора frameScratch в 0
        }
    }
}