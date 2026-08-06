using System.Runtime.CompilerServices;

namespace Verstack.Shared.Maths;

/// <summary>
/// Вещественный 2D-вектор. Value-тип (readonly struct). Для углов, скоростей, смещений
/// с плавающей точкой. Целочисленный аналог — <see cref="Vector2Int"/>.
/// </summary>
public readonly struct Vector2(float x, float y) : IEquatable<Vector2>
{
    public readonly float X = x;
    public readonly float Y = y;

    public static readonly Vector2 Zero = default;
    public static readonly Vector2 One  = new(1f, 1f);

    public float SqrMagnitude => X * X + Y * Y;

    /// <summary>Длина (с sqrt — используйте <see cref="SqrMagnitude"/> для сравнений).</summary>
    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    /// <summary>Единичный вектор того же направления; для <see cref="Zero"/> возвращает <see cref="Zero"/>.</summary>
    public Vector2 Normalized
    {
        get
        {
            var mag = SqrMagnitude;
            if (mag < 1e-10f)
                return Zero;
            var inv = 1f / MathF.Sqrt(mag);
            return new Vector2(X * inv, Y * inv);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator +(Vector2 to, Vector2 from)
        => new(to.X + from.X, to.Y + from.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 to, Vector2 from)
        => new(to.X - from.X, to.Y - from.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 to, float s)
        => new(to.X * s, to.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(float s, Vector2 to)
        => new(to.X * s, to.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 to, float s)
        => new(to.X / s, to.Y / s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 to)
        => new(-to.X, -to.Y);

    // Бит-точное equality: семантика value-type («тот же вектор»). Намеренно без epsilon —
    // приближённое сравнение вынесено в Approximately(), чтобы не ломать GetHashCode и
    // транзитивность equality. Зеркалирует подход System.Numerics.Vector3.
    // ReSharper disable CompareOfFloatsByEqualityOperator — осознанно бит-точное.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2 to, Vector2 from)
        => to.X == from.X && to.Y == from.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2 to, Vector2 from)
        => !(to == from);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";
}