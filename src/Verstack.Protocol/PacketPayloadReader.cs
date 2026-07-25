using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text;

namespace Verstack.Protocol;

/// <summary>
/// Reads fields from a packet payload sequentially.
/// </summary>
/// <remarks>
/// Operates on an already-framed payload (from <see cref="PacketFrameReader"/>).
/// A <c>false</c> return from any method means the payload is malformed.
/// </remarks>
public ref struct PacketPayloadReader
{
    private SequenceReader<byte> _reader;

    /// <summary>Bytes consumed from the payload so far.</summary>
    public long ConsumedBytes => _reader.Consumed;

    /// <summary>
    /// Initializes a reader over a packet's payload.
    /// </summary>
    /// <param name="payload">The complete frame body, as obtained from <see cref="PacketFrameReader.Current"/>.</param>
    public PacketPayloadReader(ReadOnlySequence<byte> payload)
    {
        _reader = new SequenceReader<byte>(payload);
    }

    /// <summary>Reads a VarInt (LEB128) from the payload.</summary>
    /// <returns><c>true</c> if a VarInt was fully read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadVarInt(out int value)
    {
        VarInt.ReadStatus status = VarInt.TryRead(ref _reader, out value);
        return status == VarInt.ReadStatus.Complete;
    }

    /// <summary>Reads a big-endian <see cref="ushort"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadUShortBigEndian(out ushort value)
    {
        bool ok = _reader.TryReadBigEndian(out short signed);
        value = unchecked((ushort)signed);
        return ok;
    }

    /// <summary>Reads a big-endian <see cref="long"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt64BigEndian(out long value)
        => _reader.TryReadBigEndian(out value);

    /// <summary>
    /// Reads a length-prefixed UTF-8 string (<c>VarInt(byteLength) + UTF-8 bytes</c>).
    /// </summary>
    public bool TryReadString(out string? value)
    {
        if (!TryReadVarInt(out int length))
        {
            value = null;
            return false;
        }

        if (length < 0)
        {
            value = null;
            return false;
        }

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

        value = bytes.IsSingleSegment ? Encoding.UTF8.GetString(bytes.FirstSpan) : Encoding.UTF8.GetString(bytes.ToArray());
        return true;
    }
    
    /// <summary>
    /// Reads a UUID (16 big-endian bytes, no dashes) as a <see cref="Uuid"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadUuid(out Uuid value)
    {
        if (!_reader.TryReadExact(16, out ReadOnlySequence<byte> bytes))
        {
            value = default;
            return false;
        }

        if (bytes.IsSingleSegment)
            value = Uuid.Read(bytes.FirstSpan);
        else
        {
            Span<byte> tmp = stackalloc byte[16];
            bytes.CopyTo(tmp);
            value = Uuid.Read(tmp);
        }
        return true;
    }
}