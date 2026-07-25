using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Reads VarInt-length-prefixed frames from a byte sequence. Supports <c>foreach</c> iteration.
/// </summary>
public ref struct PacketFrameReader
{
    private SequenceReader<byte> _reader;
    private readonly int _maxPacketSize;
    private readonly IPacketDecompressor? _decompressor;
    
    private ReadOnlySequence<byte> _currentPayload;
    private byte[]? _rentedBuffer;
    private SequencePosition _frameStart;

    /// <summary>The payload of the current frame. Valid only after <see cref="MoveNext"/> returns <c>true</c>.</summary>
    public ReadOnlySequence<byte> Current
    {
        get
        {
#if DEBUG
            if (Status != VarInt.ReadStatus.Complete)
                throw new InvalidOperationException($"[{nameof(PacketFrameReader)}] Current is only valid after MoveNext() returns true.");
#endif
            return _currentPayload;
        }
    }

    /// <summary>The reason why the last <see cref="MoveNext"/> call returned <c>false</c>.</summary>
    public VarInt.ReadStatus Status { get; private set; }

    /// <summary>
    /// Position to pass to <c>PipeReader.AdvanceTo</c>.
    /// </summary>
    public SequencePosition ConsumedPosition =>
        Status == VarInt.ReadStatus.Partial ? _frameStart : _reader.Position;

    /// <summary>Enables <c>foreach</c> over complete frames.</summary>
    public PacketFrameReader GetEnumerator() => this;
    
    /// <summary>
    /// Creates a reader over <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Data received from a <c>PipeReader</c>.</param>
    /// <param name="maxPacketSize">Maximum allowed payload size in bytes.</param>
    /// <param name="decompressor">Decompressor instance. If null, compression is disabled.</param>
    public PacketFrameReader(ReadOnlySequence<byte> input, int maxPacketSize = PacketFrameWriter.DEFAULT_MAX_PACKET_SIZE, IPacketDecompressor? decompressor = null)
    {
        if (maxPacketSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPacketSize), $"[{nameof(PacketFrameReader)}] maxPacketSize must be positive.");

        _reader = new SequenceReader<byte>(input);
        _maxPacketSize = maxPacketSize;
        _decompressor = decompressor;
        _currentPayload = default;
        Status = VarInt.ReadStatus.Complete;
        _frameStart = default;
        _rentedBuffer = null;
    }

    /// <summary>
    /// Advances to the next complete frame.
    /// </summary>
    /// <returns><c>true</c> if a frame is available in <see cref="Current"/></returns>
    public bool MoveNext()
    {
        // Возвращаем арендованный буфер от предыдущего кадра
        if (_rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
            _rentedBuffer = null;
        }

        _frameStart = _reader.Position;

        VarInt.ReadStatus status = VarInt.TryRead(ref _reader, out int length);
        if (status != VarInt.ReadStatus.Complete)
        {
            _currentPayload = default;
            Status = status;
            return false;
        }

        if (length < 0 || length > _maxPacketSize)
        {
            _currentPayload = default;
            Status = VarInt.ReadStatus.Malformed;
            return false;
        }

        if (_reader.Remaining < length)
        {
            _currentPayload = default;
            Status = VarInt.ReadStatus.Partial;
            return false;
        }

        if (_decompressor != null)
        {
            long beforeDataLen = _reader.Consumed;
            VarInt.ReadStatus dataStatus = VarInt.TryRead(ref _reader, out int dataLength);
            if (dataStatus != VarInt.ReadStatus.Complete)
            {
                _currentPayload = default;
                Status = VarInt.ReadStatus.Malformed;
                return false;
            }

            int dataLengthSize = (int)(_reader.Consumed - beforeDataLen);
            int compressedLength = length - dataLengthSize;

            if (dataLength == 0)
            {
                // Пакет не был сжат
                SequencePosition payloadEnd = _reader.Sequence.GetPosition(compressedLength, _reader.Position);
                _currentPayload = _reader.Sequence.Slice(_reader.Position, payloadEnd);
                _reader.Advance(compressedLength);
            }
            else
            {
                // Пакет сжат
                SequencePosition compressedEnd = _reader.Sequence.GetPosition(compressedLength, _reader.Position);
                ReadOnlySequence<byte> compressedData = _reader.Sequence.Slice(_reader.Position, compressedEnd);
                
                _rentedBuffer = ArrayPool<byte>.Shared.Rent(dataLength);
                try
                {
                    _decompressor.Decompress(compressedData, _rentedBuffer.AsSpan(0, dataLength));
                }
                catch
                {
                    _currentPayload = default;
                    Status = VarInt.ReadStatus.Malformed;
                    ArrayPool<byte>.Shared.Return(_rentedBuffer);
                    _rentedBuffer = null;
                    return false;
                }
                
                _currentPayload = new ReadOnlySequence<byte>(_rentedBuffer, 0, dataLength);
                _reader.Advance(compressedLength);
            }
        }
        else
        {
            // Обычный несжатый фрейм
            SequencePosition payloadEnd = _reader.Sequence.GetPosition(length, _reader.Position);
            _currentPayload = _reader.Sequence.Slice(_reader.Position, payloadEnd);
            _reader.Advance(length);
        }

        Status = VarInt.ReadStatus.Complete;
        return true;
    }

    /// <summary>
    /// Returns any rented decompression buffer to the pool. 
    /// Call this when finished with the reader (e.g., via <c>using</c>).
    /// </summary>
    public void Dispose()
    {
        if (_rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
            _rentedBuffer = null;
        }
    }
}