using System.Runtime.CompilerServices;

namespace Verstack.Shared.Spatial.Orientation;

public ref struct Dir8MaskExcEnumerator
{
    public Compass.Dir8 Current { get; private set; }
    
    private readonly Compass.Dir8Mask _originalMask;
    
    private Compass.Dir8Mask _remainingMask;
    private int _currentBit;
    
    internal Dir8MaskExcEnumerator(Compass.Dir8Mask mask)
    {
        _remainingMask = (Compass.Dir8Mask)(~(byte)mask & (byte)Compass.Dir8Mask.All);
        _originalMask = _remainingMask;
        _currentBit = -1;
        Current = default;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_remainingMask == 0)
            return false;
        
        _currentBit = Dir8MaskExtensions.TrailingZeroCount(_remainingMask);
        _remainingMask &= (Compass.Dir8Mask)((byte)_remainingMask - 1);
    
        Current = (Compass.Dir8)_currentBit;
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Dir8MaskExcEnumerator GetEnumerator() => this;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Count() => _originalMask.Count();
}