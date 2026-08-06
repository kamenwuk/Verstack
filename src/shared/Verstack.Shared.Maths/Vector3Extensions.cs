using System.Runtime.CompilerServices;

namespace Verstack.Shared.Maths;

public static class Vector3Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(this Vector3 to, Vector3 from)
        => to.X * from.X + to.Y * from.Y + to.Z * from.Z;
    
    /// <summary>
    /// Приближённое сравнение с допуском <paramref name="epsilon"/> — для физики/игры, где
    /// накопленные ошибки округления делают бит-точное equality бесполезным. Не является
    /// equality в смысле <see cref="object.GetHashCode"/> / Dictionary: используйте только
    /// там, где осмысленна «близость», а не идентичность.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Approximately(this Vector3 to, Vector3 from, float epsilon = 1e-5f)
        => MathF.Abs(to.X - from.X) <= epsilon
           && MathF.Abs(to.Y - from.Y) <= epsilon
           && MathF.Abs(to.Z - from.Z) <= epsilon;
}