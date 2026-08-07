using System.Runtime.CompilerServices;
using Verstack.Shared.Maths;

namespace Verstack.Shared.Spatial.Queries;

/// <summary>
/// Перечисляет все клетки прямоугольника <c>(2·half+1)²</c> с центром <c>pivot</c> —
/// сплошной обход bbox без пропусков. Pure arithmetic: только координаты клеток.
///
/// <para>Применения:</para>
/// <para>• <b>Full recompute</b> при edge-update с большой дельтой (телепорт, быстрый
/// полёт): вместо инкрементального фронта/тыла весь новый квадрат = фронт, весь старый =
/// тыл (<see cref="ChunkEdgeDelta"/>).</para>
/// <para>• <b>Initial seeding</b> viewport's игрока при входе (все начальные чанки).</para>
/// </summary>
public ref struct RectangleMaskQuery
{
    /// <summary>Текущая клетка квадрата.</summary>
    public readonly Vector2Int Current => _current;

    private readonly Vector2Int _pivot;
    private readonly int _half;
    private readonly int _boxSize;

    private int _column;
    private int _row;
    private Vector2Int _current;

    /// <summary>
    /// Создать перечислитель всех клеток прямоугольника <c>(2·half+1)²</c> с центром
    /// <paramref name="pivot"/>.
    /// </summary>
    public RectangleMaskQuery(Vector2Int pivot, int half)
    {
        _pivot = pivot;
        _half = half;
        _boxSize = half * 2 + 1;

        // Стартовое состояние: первый MoveNext сработает как перенос строки и выставит _row=0.
        _column = _boxSize - 1;
        _row = -1;
        _current = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (true)
        {
            _column++;
            if (_column >= _boxSize)
            {
                _column = 0;
                _row++;
                if (_row >= _boxSize)
                    return false;
            }

            _current = new Vector2Int(
                _pivot.X + _column - _half,
                _pivot.Y + _row - _half);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RectangleMaskQuery GetEnumerator() => this;
}