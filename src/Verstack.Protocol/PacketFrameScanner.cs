using System.Buffers;

namespace Verstack.Protocol;

/// <summary>
/// Scans a <see cref="ReadOnlySequence{T}"/> using Minecraft framing
/// (VarInt-length-prefix), yielding complete frames one at a time.
/// </summary>
/// <remarks>
/// One-shot: bound to a single <c>ReadAsync</c> chunk. After <see cref="MoveNext"/>
/// returns <c>false</c>, inspect <see cref="Status"/>:
/// <see cref="VarInt.ReadStatus.Partial"/> — hand <see cref="ConsumedPosition"/> to
/// <c>PipeReader.AdvanceTo</c> and create a fresh scanner on the next chunk;
/// <see cref="VarInt.ReadStatus.Malformed"/> — drop the connection.
/// </remarks>
public ref struct PacketFrameScanner
{
    /// <summary>Default Minecraft frame size limit, in bytes (~2 MB).</summary>
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    private SequenceReader<byte> _reader;
    private readonly int _maxPacketSize;
    private ReadOnlySequence<byte> _currentPayload;
    private VarInt.ReadStatus _status;
    private SequencePosition _frameStart;

    /// <summary>Payload of the current frame (valid only after <see cref="MoveNext"/> returns <c>true</c>).</summary>
    public ReadOnlySequence<byte> Current
    {
        get
        {
#if DEBUG
            if (_status != VarInt.ReadStatus.Complete)
                throw new InvalidOperationException($"[{nameof(PacketFrameScanner)}] Current is only valid after MoveNext() returns true.");
#endif
            return _currentPayload;
        }
    }

    /// <summary>Reason for the last <see cref="MoveNext"/> returning <c>false</c>.</summary>
    public VarInt.ReadStatus Status => _status;

    /// <summary>
    /// Position to feed to <c>PipeReader.AdvanceTo</c>. On
    /// <see cref="VarInt.ReadStatus.Partial"/> points at the start of the
    /// incomplete frame, so its bytes stay buffered for the next read.
    /// </summary>
    public SequencePosition ConsumedPosition =>
        _status == VarInt.ReadStatus.Partial ? _frameStart : _reader.Position;

    /// <summary>Supports <c>foreach</c> over complete frames.</summary>
    public PacketFrameScanner GetEnumerator() => this;
    
    /// <summary>
    /// Creates a scanner bound to <paramref name="input"/>.
    /// </summary>
    /// <param name="maxPacketSize">Upper bound on a frame's payload length;
    /// frames exceeding it are reported as <see cref="VarInt.ReadStatus.Malformed"/>.</param>
    public PacketFrameScanner(ReadOnlySequence<byte> input, int maxPacketSize = DEFAULT_MAX_PACKET_SIZE)
    {
        if (maxPacketSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPacketSize), $"[{nameof(PacketFrameScanner)}] maxPacketSize must be positive.");

        _reader = new SequenceReader<byte>(input);
        _maxPacketSize = maxPacketSize;
        _currentPayload = default;
        _status = VarInt.ReadStatus.Complete;
        _frameStart = default;
    }

    /// <summary>
    /// Advances to the next complete frame.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a complete frame is available in <see cref="Current"/>;
    /// <c>false</c> otherwise (inspect <see cref="Status"/> for the reason).
    /// </returns>
    public bool MoveNext()
    {
        // Запоминаем начало кадра: при Partial именно отсюда PipeReader должен
        // оставить данные в буфере (см. ConsumedPosition).
        _frameStart = _reader.Position;

        // VarInt-логика делегирована — сканер не знает про маски и сдвиги.
        VarInt.ReadStatus status = VarInt.TryRead(ref _reader, out int length);
        if (status != VarInt.ReadStatus.Complete)
        {
            _currentPayload = default;
            _status = status;
            return false;
        }

        // length знаковый по канону Java; отрицательная длина невозможна.
        if (length < 0 || length > _maxPacketSize)
        {
            _currentPayload = default;
            _status = VarInt.ReadStatus.Malformed;
            return false;
        }

        if (_reader.Remaining < length)
        {
            // VarInt прочитан целиком, но payload не уместился — ждём ещё данных.
            _currentPayload = default;
            _status = VarInt.ReadStatus.Partial;
            return false;
        }

        // Slice — zero-copy, даже если payload разрезан между сегментами.
        SequencePosition payloadEnd = _reader.Sequence.GetPosition(length, _reader.Position);
        _currentPayload = _reader.Sequence.Slice(_reader.Position, payloadEnd);
        _reader.Advance(length);

        _status = VarInt.ReadStatus.Complete;
        return true;
    }
}