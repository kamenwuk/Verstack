using System.Runtime.CompilerServices;
using Verstack.Shared.Maths;

namespace Verstack.Shared.Spatial.Orientation;

public static class Dir8Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly Vector2Int Delta(this Compass.Dir8 direction)
        => ref Compass.Axes[(int)direction];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2Int Translated(this Vector2Int position, Compass.Dir8 direction, int distance = 1)
        => position + Compass.Axes[(int)direction] * distance;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Translated(this Vector2 position, Compass.Dir8 direction, float distance = 1f)
        => position + Compass.UnitAxes[(int)direction] * distance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8 Opposite(this Compass.Dir8 direction)
        => (Compass.Dir8)(((int)direction + 4) % 8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOpposite(this Compass.Dir8 a, Compass.Dir8 b)
        => ((((int)a + 4) % 8) == (int)b);

    /// <summary>
    /// <remarks><see cref="Compass.Dir8.North"/>, <see cref="Compass.Dir8.East"/>,
    /// <see cref="Compass.Dir8.South"/>, <see cref="Compass.Dir8.West"/></remarks>
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCardinal(this Compass.Dir8 direction)
        => ((int)direction % 2) == 0;
    
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static Quaternion ToQuaternion(this Compass.Dir8 direction)
    // {
    //     return Quaternion.Euler(0, 0, direction.ToAngle());
    // }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToAngle(this Compass.Dir8 direction)
    {
        // Углы в градусах против часовой стрелки (т.к. в Unity угол увеличивается против ЧС)
        // North = 0°, Northeast = 45°, East = 90°, Southeast = 135°, South = 180°, Southwest = 225°, West = 270°, Northwest = 315°
        var angle = (int)direction * 45f;
        return angle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToCardinal(this Compass.Dir8 direction, out Compass.Dir8 first, out Compass.Dir8 second)
    {
        int val = (int)direction;
        if ((val & 1) == 0)
        {
            first = direction;
            second = default;
            return 1;
        }
        
        first  = (Compass.Dir8)((val - 1) & 7);
        second = (Compass.Dir8)((val + 1) & 7);
        return 2;
    }

    /// <summary>
    /// <remarks><see cref="Compass.Dir8.Northeast"/>, <see cref="Compass.Dir8.Southeast"/>,
    /// <see cref="Compass.Dir8.Southwest"/>, <see cref="Compass.Dir8.Northwest"/></remarks>
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOrdinal(this Compass.Dir8 direction)
        => ((int)direction % 2) == 1;

    /// <summary>
    /// Возвращает следующее направление в той же категории (кардинальной или ординальной).
    /// </summary>
    /// <remarks>
    /// Кардинальные: N → E → S → W → N
    /// <para>Ординальные: NE → SE → SW → NW → NE</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8 NextInCategory(this Compass.Dir8 direction)
        => (Compass.Dir8)(((int)direction + 2) & 7);
    
    
    /// <summary>
    /// Возвращает следующее направление по часовой стрелке (через 1).
    /// N → NE → E → SE → S → SW → W → NW → N
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8 NextClockwise(this Compass.Dir8 direction)
        => (Compass.Dir8)(((int)direction + 1) & 7);

    /// <summary>
    /// Возвращает следующее направление против часовой стрелки (через 1).
    /// N → NW → W → SW → S → SE → E → NE → N
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8 PrevClockwise(this Compass.Dir8 direction)
        => (Compass.Dir8)(((int)direction + 7) & 7);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8 NearestDirection(float angleDegrees)
    {
        var normalized = angleDegrees % 360f;
        if (normalized < 0) normalized += 360f;

        var octant = (int)((normalized + 22.5f) / 45f) % 8;
        return (Compass.Dir8)octant;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8Mask Mask(this Compass.Dir8 direction)
        => (Compass.Dir8Mask)(1 << (int)direction);
}
