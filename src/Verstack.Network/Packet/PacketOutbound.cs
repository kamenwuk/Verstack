using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;

namespace Verstack.Network.Packet;

public ref struct PacketOutbound
{
    private readonly NetworkChannel _channel;
    private readonly IPacketCompressor _compressor;
    private readonly Span<byte> _frameScratch;
    private readonly Span<byte> _payloadBuffer;
    private int _frameOffset;

#if DEBUG
    private bool _isWriting;
#endif

    public PacketOutbound(
        NetworkChannel channel, 
        IPacketCompressor compressor, 
        Span<byte> frameScratch, 
        Span<byte> payloadBuffer)
    {
        _channel = channel;
        _compressor = compressor;
        _frameScratch = frameScratch;
        _payloadBuffer = payloadBuffer;
        _frameOffset = 0;
#if DEBUG
        _isWriting = false;
#endif
    }

    public PacketStreamWriter Begin()
    {
#if DEBUG
        if (_isWriting)
            throw new InvalidOperationException("Begin() called without Committing the previous packet!");
        _isWriting = true;
#endif
        return new PacketStreamWriter(_payloadBuffer);
    }

    public void Commit(scoped ref PacketStreamWriter streamWriter)
    {
        var frameWriter = new PacketStreamWriter(_frameScratch[_frameOffset..]);
        PacketFrame.Write(ref frameWriter, streamWriter.WrittenSpan, _compressor, _channel.CompressionThreshold);
        
        _frameOffset += frameWriter.Written;
        streamWriter.Reset();
        
#if DEBUG
        _isWriting = false;
#endif
    }

    /// <summary>
    /// Сбрасывает накопленный framing-буфер в канал.
    /// В DEBUG кидает исключение, если забыли вызвать Commit.
    /// </summary>
    public void Flush()
    {
#if DEBUG
        if (_isWriting)
            throw new InvalidOperationException("Flush() called, but a packet writer was not committed!");
#endif

        if (_frameOffset > 0)
        {
            _channel.EnqueueOutbound(_frameScratch[.._frameOffset]);
            _frameOffset = 0;
        }
    }

    public void EnableCompression(int threshold) => _channel.CompressionThreshold = threshold;
}