using System.Runtime.CompilerServices;

namespace Verstack.Shared.Maths;

/// <summary>
/// Целочисленный 3D-вектор. Value-тип (readonly struct). Базовый math-примитив для
/// блочных и мировых координат (Voxel), индексов секций и т.п. Плоский 2D-аналог —
/// <see cref="Vector2Int"/>.
/// </summary>
public readonly struct Vector3Int(int x, int y, int z) : IEquatable<Vector3Int>
{
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Z = z;

    public static readonly Vector3Int Zero = default;
    public static readonly Vector3Int One = new(1, 1, 1);

    public int SqrMagnitude => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator +(Vector3Int to, Vector3Int from)
        => new(to.X + from.X, to.Y + from.Y, to.Z + from.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator -(Vector3Int to, Vector3Int from)
        => new(to.X - from.X, to.Y - from.Y, to.Z - from.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator *(Vector3Int to, int s)
        => new(to.X * s, to.Y * s, to.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator *(int s, Vector3Int to)
        => new(to.X * s, to.Y * s, to.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int operator -(Vector3Int to)
        => new(-to.X, -to.Y, -to.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3Int to, Vector3Int from)
        => to.X == from.X && to.Y == from.Y && to.Z == from.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3Int to, Vector3Int from)
        => !(to == from);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector3Int other)
        => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vector3Int v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}