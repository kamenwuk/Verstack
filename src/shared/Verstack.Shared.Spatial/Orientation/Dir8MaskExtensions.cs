using System.Runtime.CompilerServices;

namespace Verstack.Shared.Spatial.Orientation;

public static class Dir8MaskExtensions
{
    private static readonly byte[] TrailingZeroTable = new byte[256];
    private static readonly byte[] PopCountTable = new byte[256];
    
    static Dir8MaskExtensions()
    {
        for (var i = 0; i < 256; i++)
        {
            byte trailing = 0;
            while (trailing < 8 && (i & (1 << trailing)) == 0)
                trailing++;
            TrailingZeroTable[i] = trailing;
            
            byte count = 0;
            var v = i;
            while (v != 0)
            {
                if ((v & 1) != 0) count++;
                v >>= 1;
            }
            PopCountTable[i] = count;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int TrailingZeroCount(Compass.Dir8Mask value)
    {
        return value == 0 ? 8 : TrailingZeroTable[(byte)value];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8Mask Add(this Compass.Dir8Mask mask, Compass.Dir8 direction)
        => mask | (Compass.Dir8Mask)(1 << (int)direction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8Mask Add(this Compass.Dir8Mask mask, Compass.Dir8Mask other)
        => mask | other;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8Mask Del(this Compass.Dir8Mask mask, Compass.Dir8 direction)
        => mask & ~(Compass.Dir8Mask)(1 << (int)direction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Compass.Dir8Mask Del(this Compass.Dir8Mask mask, Compass.Dir8Mask other)
        => mask & ~other;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Has(this Compass.Dir8Mask mask, Compass.Dir8 direction)
        => (mask & (Compass.Dir8Mask)(1 << (int)direction)) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAny(this Compass.Dir8Mask mask, Compass.Dir8Mask other)
        => (mask & other) != 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(this Compass.Dir8Mask mask)
    {
        return PopCountTable[(byte)mask];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAll(this Compass.Dir8Mask mask, Compass.Dir8Mask other)
        => (mask & other) == other;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetIncFirst(this Compass.Dir8Mask mask, out Compass.Dir8 direction)
    {
        if (mask == 0)
        {
            direction = default;
            return false;
        }
        direction = (Compass.Dir8)TrailingZeroCount(mask);
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetExcFirst(this Compass.Dir8Mask mask, out Compass.Dir8 direction)
    {
        var excMask = (Compass.Dir8Mask)(~(byte)mask & (byte)Compass.Dir8Mask.All);
        if (excMask == 0)
        {
            direction = default;
            return false;
        }
        direction = (Compass.Dir8)TrailingZeroCount(excMask);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dir8MaskIncEnumerator Inc(this Compass.Dir8Mask mask) 
        => new Dir8MaskIncEnumerator(mask);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dir8MaskExcEnumerator Exc(this Compass.Dir8Mask mask) 
        => new Dir8MaskExcEnumerator(mask);
}



