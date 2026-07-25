using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Verstack.Protocol;

/// <summary>
/// A Minecraft UUID: 128 bits on the wire as 16 big-endian bytes (no dashes),
/// independent of <see cref="System.Guid"/>'s mixed-endian layout.
/// </summary>
/// <remarks>
/// Stored as two <see cref="ulong"/>s in wire (big-endian) order, so byte-for-byte
/// comparisons against the wire stay trivial. <see cref="System.Guid"/> treats its
/// first three fields as little-endian, which would silently break any code that
/// assumes the in-memory layout matches the wire — hence a dedicated type.
/// </remarks>
public readonly struct Uuid : IEquatable<Uuid>
{
    // Первые 8 байт провода как big-endian uint64.
    private readonly ulong _hi;
    // Последние 8 байт провода как big-endian uint64.
    private readonly ulong _lo;

    private Uuid(ulong hi, ulong lo)
    {
        _hi = hi;
        _lo = lo;
    }

    /// <summary>
    /// Reads a UUID from exactly 16 bytes, big-endian.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is shorter than 16.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Uuid Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
            throw new ArgumentException($"[{nameof(Uuid)}] Need 16 bytes, got {bytes.Length}.", nameof(bytes));

        ulong hi = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        ulong lo = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        return new Uuid(hi, lo);
    }

    /// <summary>
    /// Writes the UUID as 16 big-endian bytes into <paramref name="bytes"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is shorter than 16.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(Span<byte> bytes)
    {
        if (bytes.Length < 16)
            throw new ArgumentException($"[{nameof(Uuid)}] Need 16 bytes, got {bytes.Length}.", nameof(bytes));

        BinaryPrimitives.WriteUInt64BigEndian(bytes, _hi);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], _lo);
    }

    /// <inheritdoc/>
    public bool Equals(Uuid other) => _hi == other._hi && _lo == other._lo;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is Uuid other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_hi, _lo);

    /// <inheritdoc/>
    /// <remarks>
    /// Canonical dashed form: <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>, lowercase.
    /// One string allocation (unavoidable for text); the rest is stackalloc.
    /// </remarks>
    public override string ToString()
    {
        Span<byte> b = stackalloc byte[16];
        Write(b);

        Span<char> c = stackalloc char[36];
        int ci = 0;
        for (int i = 0; i < 16; i++)
        {
            // Дефисы по RFC 4122: после байтов 4, 6, 8, 10.
            if (i == 4 || i == 6 || i == 8 || i == 10)
                c[ci++] = '-';
            byte v = b[i];
            c[ci++] = HexDigit(v >> 4);
            c[ci++] = HexDigit(v & 0x0F);
        }
        return new string(c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static char HexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
    }

    /// <inheritdoc/>
    public static bool operator ==(Uuid left, Uuid right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Uuid left, Uuid right) => !left.Equals(right);
}