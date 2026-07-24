using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text;

namespace Verstack.Protocol;

/// <summary>
/// Consumes the payload of a single Minecraft packet field-by-field.
/// The reading-side counterpart to the writing logic in
/// <see cref="PacketFraming"/> / packet serializers.
/// </summary>
/// <remarks>
/// <para>Lives in the handler's synchronous section (before <c>FlushAsync</c>):
/// the payload is already framed by <see cref="PacketFrameScanner"/>, so this
/// reader walks a complete frame, not a raw stream.</para>
/// <para>Why <see langword="ref struct"/>: wraps a <see cref="SequenceReader{T}"/>
/// by value — the cursor cannot escape the stack. That matches
/// <see cref="PacketFrameScanner"/> and keeps the read path zero-allocation.</para>
/// <para>Why <see langword="bool"/> instead of <see cref="VarInt.ReadStatus"/>:
/// <see cref="PacketFrameScanner"/> is an outer loop that must distinguish
/// <c>Partial</c> (wait for more bytes) from <c>Malformed</c> (drop) for
/// backpressure. <see cref="PacketReader"/> runs on an already-complete frame —
/// a short read here means the client sent a malformed packet, not «need more
/// data». One outcome (<see langword="false"/>) is enough; the caller treats it
/// as a bad packet.</para>
/// </remarks>
public ref struct PacketReader
{
    private SequenceReader<byte> _reader;

    /// <summary>Initializes the reader over one packet's payload bytes.</summary>
    public PacketReader(ReadOnlySequence<byte> payload)
    {
        _reader = new SequenceReader<byte>(payload);
    }

    /// <summary>Bytes consumed so far from the payload.</summary>
    public long ConsumedBytes => _reader.Consumed;
    
    /// <summary>
    /// Reads a VarInt (LEB128) field. Transparently crosses segment boundaries.
    /// </summary>
    /// <returns><see langword="true"/> if a whole VarInt was read;
    /// <see langword="false"/> if the frame ended mid-VarInt (malformed packet).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVarInt(out int value)
    {
        VarInt.ReadStatus status = VarInt.TryRead(ref _reader, out value);
        return status == VarInt.ReadStatus.Complete;
    }

    /// <summary>
    /// Reads a big-endian <see cref="ushort"/> (2 bytes, fixed width).
    /// Used for fixed-width fields like the Handshake port.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadUShortBigEndian(out ushort value)
    {
        bool ok = _reader.TryReadBigEndian(out short signed);
        // unchecked: побитовое переинтерпретирование signed → unsigned.
        // Порт >32767 (например SRV-записи на 49152+) представлен в signed как
        // отрицательное число (доп. до 2); unchecked сохраняет битовый паттерн.
        value = unchecked((ushort)signed);
        return ok;
    }

    /// <summary>
    /// Reads a big-endian <see cref="long"/> (8 bytes, fixed width).
    /// Used for the Ping/Pong timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt64BigEndian(out long value)
        => _reader.TryReadBigEndian(out value);

    /// <summary>
    /// Reads a Minecraft length-prefixed UTF-8 string:
    /// <c>[VarInt(byteLength)][UTF-8 bytes]</c>.
    /// </summary>
    /// <remarks>
    /// Allocates a <see cref="string"/> for the result — unavoidable for text.
    /// Not a hot path (1-2 string fields per handshake/login); the hot path
    /// (chunks, entities) carries no length-prefixed strings.
    /// </remarks>
    public bool TryReadString(out string? value)
    {
        if (!TryReadVarInt(out int length))
        {
            value = null;
            return false;
        }

        // Отрицательная длина невозможна у валидного клиента, но VarInt может
        // раскодировать отрицательное число (sign-extension в 5-м байте).
        if (length < 0)
        {
            value = null;
            return false;
        }

        // Заявленная длина превышает остаток кадра → клиент прислал мусор.
        long remaining = _reader.Length - _reader.Consumed;
        if (length > remaining)
        {
            value = null;
            return false;
        }

        if (!_reader.TryReadExact(length, out ReadOnlySequence<byte> bytes))
        {
            value = null;
            return false;
        }

        // Contiguous → zero-copy decode. Segmented → одна аллокация под копию.
        // Сегментированный payload в практике редок (короткие строки handshake),
        // но деградирует корректно, а не падает.
        value = bytes.IsSingleSegment
            ? Encoding.UTF8.GetString(bytes.FirstSpan) : Encoding.UTF8.GetString(bytes.ToArray());
        return true;
    }
}