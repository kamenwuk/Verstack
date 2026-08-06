using System.Runtime.CompilerServices;

namespace Verstack.Shared.Maths;

/// <summary>
/// Вещественный 3D-вектор. Value-тип (readonly struct). Для мировых позиций сущностей,
/// скоростей, направлений с плавающей точкой. Целочисленный аналог — <see cref="Vector3Int"/>.
/// </summary>
public readonly struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Z = z;

    public static readonly Vector3 Zero = default;
    public static readonly Vector3 One = new(1f, 1f, 1f);
    public static readonly Vector3 Up = new(0f,  1f, 0f);
    public static readonly Vector3 Down = new(0f, -1f, 0f);

    public float SqrMagnitude => X * X + Y * Y + Z * Z;

    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    public Vector3 Normalized
    {
        get
        {
            var mag = SqrMagnitude;
            if (mag < 1e-10f)
                return Zero;
            var inv = 1f / MathF.Sqrt(mag);
            return new Vector3(X * inv, Y * inv, Z * inv);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator +(Vector3 to, Vector3 from)
        => new(to.X + from.X, to.Y + from.Y, to.Z + from.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 to, Vector3 from)
        => new(to.X - from.X, to.Y - from.Y, to.Z - from.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 to, float s)
        => new(to.X * s, to.Y * s, to.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(float s, Vector3 to)
        => new(to.X * s, to.Y * s, to.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 to, float s)
        => new(to.X / s, to.Y / s, to.Z / s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 to)
        => new(-to.X, -to.Y, -to.Z);

    // Бит-точное equality: семантика value-type («тот же вектор»). Намеренно без epsilon —
    // приближённое сравнение вынесено в Approximately(), чтобы не ломать GetHashCode и
    // транзитивность equality. Зеркалирует подход System.Numerics.Vector3.
    // ReSharper disable CompareOfFloatsByEqualityOperator — осознанно бит-точное.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3 to, Vector3 from)
        => to.X == from.X && to.Y == from.Y && to.Z == from.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3 to, Vector3 from)
        => !(to == from);
    // ReSharper restore CompareOfFloatsByEqualityOperator

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector3 other)
        => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}