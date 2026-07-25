using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Reads VarInt-length-prefixed frames from a byte sequence. Supports <c>foreach</c> iteration.
/// </summary>
/// <remarks>
/// Bound to a single data block (from <c>PipeReader.ReadAsync</c>).
/// When <see cref="MoveNext"/> returns <c>false</c>, check <see cref="Status"/>:
/// <list type="bullet">
/// <item><see cref="VarInt.ReadStatus.Partial"/> — call <c>PipeReader.AdvanceTo</c> with
/// <see cref="ConsumedPosition"/>, then create a new <c>PacketFrameReader</c> for the next block;</item>
/// <item><see cref="VarInt.ReadStatus.Malformed"/> — drop the connection.</item>
/// </list>
/// </remarks>
public ref struct PacketFrameReader
{
    private SequenceReader<byte> _reader;
    private readonly int _maxPacketSize;
    private ReadOnlySequence<byte> _currentPayload;
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

    /// <summary>The reason why the last <see cref="MoveNext"/> call returned <c>false</c>.</summary
    public VarInt.ReadStatus Status { get; private set; }

    /// <summary>
    /// Position to pass to <c>PipeReader.AdvanceTo</c>.
    /// When <see cref="VarInt.ReadStatus.Partial"/>, points to the start of the incomplete frame
    /// so its bytes are retained in the buffer.
    /// </summary>
    public SequencePosition ConsumedPosition =>
        Status == VarInt.ReadStatus.Partial ? _frameStart : _reader.Position;

    /// <summary>Enables <c>foreach</c> over complete frames.</summary>
    public PacketFrameReader GetEnumerator() => this;
    
    /// <summary>
    /// Creates a reader over <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Data received from a <c>PipeReader</c>.</param>
    /// <param name="maxPacketSize">Maximum allowed payload size in bytes (default 2 MB).</param>
    public PacketFrameReader(ReadOnlySequence<byte> input, int maxPacketSize = PacketFrameWriter.DEFAULT_MAX_PACKET_SIZE)
    {
        if (maxPacketSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPacketSize), $"[{nameof(PacketFrameReader)}] maxPacketSize must be positive.");

        _reader = new SequenceReader<byte>(input);
        _maxPacketSize = maxPacketSize;
        _currentPayload = default;
        Status = VarInt.ReadStatus.Complete;
        _frameStart = default;
    }

    /// <summary>
    /// Advances to the next complete frame.
    /// </summary>
    /// <returns><c>true</c> if a frame is available in <see cref="Current"/> (otherwise check <see cref="Status"/>)</returns>
    public bool MoveNext()
    {
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

        SequencePosition payloadEnd = _reader.Sequence.GetPosition(length, _reader.Position);
        _currentPayload = _reader.Sequence.Slice(_reader.Position, payloadEnd);
        _reader.Advance(length);

        Status = VarInt.ReadStatus.Complete;
        return true;
    }
}