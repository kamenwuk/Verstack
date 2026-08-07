using Verstack.Shared.Spatial.Orientation;
using System.Runtime.CompilerServices;
using Verstack.Shared.Maths;

namespace Verstack.Shared.Spatial.Queries;

/// <summary>
/// Перечисляет клетки «грани» прямоугольника <c>(2·half+1)²</c> с центром <c>pivot</c>,
/// обращённой в заданное направление. Pure arithmetic: только координаты клеток.
///
/// <para>Два режима в зависимости от типа направления:</para>
/// <para><b>Кардинальное (N/E/S/W)</b> — полоса клеток перпендикулярно направлению:
/// прямая линия длиной <c>2·half+1</c> на расстоянии <c>half</c> от центра.</para>
/// <para><b>Ординальное (NE/SE/SW/NW)</b> — «угол» прямоугольника: две смежные грани,
/// всего <c>4·half+1</c> клеток.</para>
///
/// <para>Если <c>isOutline = true</c>, <see cref="Current"/> возвращает не саму граничную
/// клетку, а её соседа в направлении <c>direction</c> — контурную клетку снаружи
/// прямоугольника. Используется для edge-update фронта: при движении объекта на один шаг
/// новые клетки = outline старого объекта в направлении движения.</para>
/// </summary>
public ref struct RectangleEdgeLineQuery
{
    /// <summary>Текущая клетка грани (или контурная, если <c>isOutline</c>).</summary>
    public readonly Vector2Int Current => _isOutline
        ? _cursor + _edgeDirection.Delta()
        : _cursor;

    private readonly Compass.Dir8 _edgeDirection;
    private readonly int _half;
    private readonly bool _isOutline;
    private readonly Vector2Int _pivot;

    // Кардинальный режим: движение перпендикулярно грани, шаг за шагом.
    private Compass.Dir8 _perpendicularDirection;
    private int _perpendicularOffset;   // от −half до +half; старт = −half−1 (виртуальная точка до итерации).
    private int _currentEdgeDepth;      // глубина грани по edgeDirection; для прямоугольника = half после первого шага.
    
    // Ординальный режим: два кардинальных компонента ординального направления.
    // Пример для NE: _firstCardinal = N, _secondCardinal = E.
    private Compass.Dir8 _firstCardinal;
    private Compass.Dir8 _secondCardinal;
    private int _phaseIndex;            // шаговый счётчик.

    private Vector2Int _cursor;

    /// <summary>
    /// Создать перечислитель грани прямоугольника <c>(2·half+1)²</c> с центром
    /// <paramref name="pivot"/>, обращённой в <paramref name="direction"/>.
    /// </summary>
    public RectangleEdgeLineQuery(Vector2Int pivot, int half, Compass.Dir8 direction, bool isOutline = false)
    {
        this = default;  // обнуляет все поля; ниже выставляем только нужные для режима.
        _edgeDirection = direction;
        _isOutline = isOutline;
        _half = half;
        _pivot = pivot;

        if (direction.IsCardinal())
            SetupCardinalEdge();
        else
            SetupOrdinalEdge();
    }

    private void SetupCardinalEdge()
    {
        // Перпендикулярное — следующее кардинальное по часовой: N→E, E→S, S→W, W→N.
        _perpendicularDirection = _edgeDirection.NextInCategory();

        // Счётчик начинается с −half−1: первый MoveNext сдвинет до −half (первая клетка полосы).
        _perpendicularOffset = -_half - 1;

        // Курсор ставим на одну позицию раньше стартовой — виртуальная точка «до итерации»:
        // (half+1) шагов от пивота назад по перпендикуляру.
        var back = _perpendicularDirection.Opposite();
        _cursor = _pivot + back.Delta() * (_half + 1);
    }

    private void SetupOrdinalEdge()
    {
        // Ординальное направление = два кардинальных компонента:
        //   NE = PrevClockwise(NE)=N  + NextClockwise(NE)=E
        //   NW = PrevClockwise(NW)=W  + NextClockwise(NW)=N
        _firstCardinal = _edgeDirection.PrevClockwise();
        _secondCardinal = _edgeDirection.NextClockwise();

        // Стартовая клетка — дальний угол первой грани:
        //   1) отступаем назад по перпендикуляру к _firstCardinal на half шагов
        //   2) затем вперёд по _firstCardinal на half шагов
        // Пример NE, half=1: pivot → (pivot−E) → pivot−E+N = угол NW прямоугольника.
        var back = _firstCardinal.NextInCategory().Opposite();
        _cursor = _pivot + back.Delta() * _half + _firstCardinal.Delta() * _half;
        _phaseIndex = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_edgeDirection.IsOrdinal())
            return AdvanceOrdinalCorner();
        return AdvanceCardinal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool AdvanceCardinal()
    {
        _perpendicularOffset++;
        if (_perpendicularOffset > _half)
            return false;

        // Шаг перпендикулярно грани.
        _cursor += _perpendicularDirection.Delta();

        // Глубина грани по edgeDirection. Для прямоугольника — константа half; первый шаг
        // сдвигает курсор от пивота к самой грани (delta = half − 0), далее delta = 0.
        // (В Fortress здесь же — Пифагор для круга; прямоугольник упрощён до константы.)
        var newDepth = _half;
        var delta = newDepth - _currentEdgeDepth;
        if (delta != 0)
            _cursor += _edgeDirection.Delta() * delta;
        _currentEdgeDepth = newDepth;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool AdvanceOrdinalCorner()
    {
        // Всего клеток угла: первая грань (2·half+1) + вторая грань (2·half) = 4·half+1,
        // _phaseIndex идёт 0 … 4·half включительно (сентинель 0 — стартовая клетка, без движения).
        if (_phaseIndex > _half * 4)
            return false;

        if (_phaseIndex > 0)
        {
            // Границу смены грани определяем без отдельного поля:
            //   шаги 1 … 2·half   → двигаемся по _firstCardinal.NextInCategory()
            //   шаги 2·half+1 … 4·half → двигаемся по _secondCardinal.NextInCategory()
            var dir = _phaseIndex <= _half * 2
                ? _firstCardinal.NextInCategory()
                : _secondCardinal.NextInCategory();
            _cursor += dir.Delta();
        }
        _phaseIndex++;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RectangleEdgeLineQuery GetEnumerator() => this;
}