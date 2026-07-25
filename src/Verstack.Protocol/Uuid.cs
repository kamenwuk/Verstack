using System.Buffers.Binary;
using System.Runtime.CompilerServices;

/// <summary>
/// A Minecraft UUID: 128 bits, transmitted as 16 big-endian bytes without dashes.
/// </summary>
public readonly struct Uuid : IEquatable<Uuid>
{
    private readonly ulong _hi;
    private readonly ulong _lo;

    private Uuid(ulong hi, ulong lo)
    {
        _hi = hi;
        _lo = lo;
    }

    /// <summary>Reads a UUID from 16 big-endian bytes.</summary>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is shorter than 16.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Uuid Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
            throw new ArgumentException($"Need 16 bytes, got {bytes.Length}.", nameof(bytes));

        ulong hi = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        ulong lo = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        return new Uuid(hi, lo);
    }

    /// <summary>Writes the UUID as 16 big-endian bytes.</summary>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is shorter than 16.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Span<byte> bytes)
    {
        if (bytes.Length < 16)
            throw new ArgumentException($"Need 16 bytes, got {bytes.Length}.", nameof(bytes));

        BinaryPrimitives.WriteUInt64BigEndian(bytes, _hi);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], _lo);
    }

    /// <inheritdoc/>
    public bool Equals(Uuid other) => _hi == other._hi && _lo == other._lo;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Uuid other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_hi, _lo);

    /// <summary>Returns the UUID in standard dashed format (e.g. 550e8400-e29b-41d4-a716-446655440000).</summary>
    public override string ToString()
    {
        Span<byte> bytes = stackalloc byte[16];
        Write(bytes);

        Span<char> chars = stackalloc char[36];
        int ci = 0;
        for (int i = 0; i < 16; i++)
        {
            if (i == 4 || i == 6 || i == 8 || i == 10)
                chars[ci++] = '-';
            byte v = bytes[i];
            chars[ci++] = HexDigit(v >> 4);
            chars[ci++] = HexDigit(v & 0x0F);
        }
        return new string(chars);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static char HexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
    }

    public static bool operator ==(Uuid left, Uuid right) => left.Equals(right);

    public static bool operator !=(Uuid left, Uuid right) => !left.Equals(right);
}