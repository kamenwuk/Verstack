using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using System.Buffers;

namespace Verstack.Network.Packet;

/// <summary>
/// Управляет жизненным циклом формирования исходящих пакетов: арендой буферов, фреймингом и постановкой в очередь отправки.
/// </summary>
/// <remarks>
/// <para>
/// Реализует паттерн <c>Begin/Commit/Flush</c>. 
/// Сначала арендуется буфер для полезной нагрузки (<c>Begin</c>), затем данные фреймируются и сжимаются (<c>Commit</c>), 
/// и, наконец, готовый кадр передаётся в очередь отправки (<c>Flush</c>).
/// </para>
/// <para>
/// Поддерживает батчинг: несколько вызовов <c>Begin/Commit</c> могут накапливаться в одном фреймовом буфере 
/// перед вызовом <c>Flush</c>, что уменьшает количество системных вызовов при отправке.
/// </para>
/// <para>
/// При вызове <c>Flush</c> происходит передача владения массивом очереди отправки (zero-copy). 
/// Внутренняя ссылка обнуляется, чтобы предотвратить двойной возврат массива в пул.
/// </para>
/// </remarks>
public ref struct PacketOutbound : IDisposable
{
    private const int INITIAL_PAYLOAD_SIZE = 2048;
    private const int INITIAL_FRAME_SIZE = 4096;

    private readonly NetworkChannel _channel;
    private readonly IPacketCompressor _compressor;
    
    private byte[] _payloadArray;
    private byte[] _frameArray;
    private int _frameOffset;

#if DEBUG
    private bool _isWriting;
#endif

    /// <summary>
    /// Инициализирует экземпляр с привязкой к сетевому каналу и компрессору.
    /// </summary>
    internal PacketOutbound(NetworkChannel channel, IPacketCompressor compressor)
    {
        _channel = channel;
        _compressor = compressor;
        _frameArray = null;
        _payloadArray = null;
        _frameOffset = 0;
    }

    /// <summary>
    /// Начинает формирование нового пакета. Арендует буфер для полезной нагрузки.
    /// </summary>
    /// <returns>Писатель для записи сырых данных пакета.</returns>
    public PacketStreamWriter Begin()
    {
#if DEBUG
        if (_isWriting)
            throw new InvalidOperationException("Begin() called without Committing the previous packet!");
        _isWriting = true;
#endif
        _payloadArray ??= ArrayPool<byte>.Shared.Rent(INITIAL_PAYLOAD_SIZE);
        return new PacketStreamWriter(_payloadArray);
    }

    /// <summary>
    /// Завершает формирование пакета. Применяет фрейминг и сжатие к записанным данным и добавляет результат во фреймовый буфер.
    /// </summary>
    /// <param name="streamWriter">Писатель, полученный из <c>Begin()</c>.</param>
    public void Commit(scoped ref PacketStreamWriter streamWriter)
    {
        // Если payload вырос внутри streamWriter, обновляем ссылку у себя
        _payloadArray = streamWriter.Buffer;

        _frameArray ??= ArrayPool<byte>.Shared.Rent(INITIAL_FRAME_SIZE);

        var frameWriter = new PacketStreamWriter(_frameArray, _frameOffset);
        PacketFrame.Write(ref frameWriter, streamWriter.WrittenSpan, _compressor, _channel.CompressionThreshold);
        
        // Если framing-буфер вырос внутри PacketFrame.Write, обновляем ссылку!
        _frameArray = frameWriter.Buffer;
        _frameOffset = frameWriter.Written;
        
        streamWriter.Reset();
        
#if DEBUG
        _isWriting = false;
#endif
    }

    /// <summary>
    /// Передаёт накопленные фреймы в очередь отправки сетевого канала. 
    /// Передаёт владение массивом без копирования (zero-copy).
    /// </summary>
    public void Flush()
    {
#if DEBUG
        if (_isWriting)
            throw new InvalidOperationException("Flush() called, but a packet writer was not committed!");
#endif

        if (_frameOffset > 0 && _frameArray != null)
        {
            // ZERO-COPY: Передаем массив напрямую в очередь! Send-воркер вернет его в пул.
            _channel.EnqueueOutbound(_frameArray, _frameOffset);
            
            // Мы отдали владение, очищаем ссылку, чтобы Dispose не вернул его в пул дважды
            _frameArray = null;
            _frameOffset = 0;
        }
    }

    /// <summary>
    /// Устанавливает порог сжатия для исходящих пакетов.
    /// </summary>
    public void EnableCompression(int threshold) => _channel.CompressionThreshold = threshold;

    /// <summary>
    /// Сбрасывает оставшиеся данные (вызывает <c>Flush</c>) и возвращает арендованные буферы в пул, если они не были переданы в очередь.
    /// </summary>
    public void Dispose()
    {
        Flush();
        
        if (_payloadArray != null)
            ArrayPool<byte>.Shared.Return(_payloadArray);
            
        if (_frameArray != null)
            ArrayPool<byte>.Shared.Return(_frameArray);
    }
}