using System.Runtime.CompilerServices;

namespace Verstack.Shared.Maths;

/// <summary>
/// Целочисленный 2D-вектор. Value-тип (readonly struct): операторы возвращают новый
/// экземпляр, поля неизменяемы. Базовый math-примитив для координат сеток (чанки,
/// тайлы, 2D-позиции). Для блочных/мировых 3D-координат см. <see cref="Vector3Int"/>.
/// </summary>
public readonly struct Vector2Int(int x, int y) : IEquatable<Vector2Int>
{
    public readonly int X = x;
    public readonly int Y = y;

    public static readonly Vector2Int Zero  = default;
    public static readonly Vector2Int Up = new(0,  1);
    public static readonly Vector2Int Down = new(0, -1);
    public static readonly Vector2Int Left = new(-1, 0);
    public static readonly Vector2Int Right = new(1,  0);
    public static readonly Vector2Int One = new(1, 1);

    /// <summary>Квадрат длины (без sqrt — для сравнений расстояний без потери точности).</summary>
    public int SqrMagnitude => X * X + Y * Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator +(Vector2Int to, Vector2Int from)
        => new(to.X + from.X, to.Y + from.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator -(Vector2Int to, Vector2Int from)
        => new(to.X - from.X, to.Y - from.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator *(Vector2Int to, int s)
        => new(to.X * s, to.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator *(int s, Vector2Int to)
        => new(to.X * s, to.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int operator -(Vector2Int to)
        => new(-to.X, -to.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2Int to, Vector2Int from)
        => to.X == from.X && to.Y == from.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2Int to, Vector2Int from)
        => !(to == from);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vector2Int v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";
}