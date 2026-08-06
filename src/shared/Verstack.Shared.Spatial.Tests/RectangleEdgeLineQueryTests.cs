using Verstack.Shared.Spatial.Orientation;
using Verstack.Shared.Spatial.Queries;
using Verstack.Shared.Maths;

namespace Verstack.Shared.Spatial.Tests;

/// <summary>
/// Тесты <see cref="RectangleEdgeLineQuery"/>: размеры граней (кардинал 2·half+1,
/// ординал 4·half+1), точные координаты клеток для всех 8 направлений, поведение
/// <c>isOutline</c> (сдвиг контура наружу). Контрольные координаты вычислены вручную для
/// pivot (0,0) и half=2 — проверяют, что грань действительно лежит на расстоянии half от
/// центра, а outline — на half+1.
/// </summary>
public class RectangleEdgeLineQueryTests
{
    // ─────────────────────  Размеры граней  ─────────────────────

    [Theory]
    [InlineData(Compass.Dir8.North, 5)]   // кардинал: 2·2+1
    [InlineData(Compass.Dir8.East,  5)]
    [InlineData(Compass.Dir8.South, 5)]
    [InlineData(Compass.Dir8.West,  5)]
    [InlineData(Compass.Dir8.Northeast, 9)]  // ординал: 4·2+1
    [InlineData(Compass.Dir8.Southeast, 9)]
    [InlineData(Compass.Dir8.Southwest, 9)]
    [InlineData(Compass.Dir8.Northwest, 9)]
    public void Count_MatchesShapeFormula(Compass.Dir8 dir, int expected)
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, dir);
        var count = 0;
        foreach (var _ in query)
            count++;
        Assert.Equal(expected, count);
    }

    // ─────────────────────  Кардинал East — точные координаты  ─────────────────────

    /// <summary>
    /// East-грань квадрата 5×5 с центром (0,0): столбец x=+2, z от −2 до +2.
    /// Без outline — сама грань (тыл при движении на запад).
    /// </summary>
    [Fact]
    public void EastEdge_NoOutline_IsColumnX2()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.East);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        foreach (var c in cells)
            Assert.Equal(2, c.X);
        Assert.Equal(new Vector2Int(2, -2), cells[0]);
        Assert.Equal(new Vector2Int(2,  2), cells[^1]);
    }

    /// <summary>
    /// East-грань с outline: столбец x=+3 (контур снаружи, фронт при движении на восток).
    /// </summary>
    [Fact]
    public void EastEdge_Outline_IsColumnX3()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.East, isOutline: true);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        foreach (var c in cells)
            Assert.Equal(3, c.X);
    }

    // ─────────────────────  Кардинал North — точные координаты  ─────────────────────

    /// <summary>
    /// North-грань (z=−2): строка z=−2, x от −2 до +2. North = (0,−1) → грань со стороны
    /// уменьшения Z (как в Minecraft: север = −Z).
    /// </summary>
    [Fact]
    public void NorthEdge_NoOutline_IsRowZMinus2()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.North);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        foreach (var c in cells)
            Assert.Equal(-2, c.Y);
    }

    /// <summary>North-грань с outline: строка z=−3.</summary>
    [Fact]
    public void NorthEdge_Outline_IsRowZMinus3()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.North, isOutline: true);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        foreach (var c in cells)
            Assert.Equal(-3, c.Y);
    }

    // ─────────────────────  Кардинал South/West — направление сдвига  ─────────────────────

    /// <summary>South-грань без outline: строка z=+2.</summary>
    [Fact]
    public void SouthEdge_NoOutline_IsRowZ2()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.South);
        foreach (var c in query)
            Assert.Equal(2, c.Y);
    }

    /// <summary>West-грань без outline: столбец x=−2.</summary>
    [Fact]
    public void WestEdge_NoOutline_IsColumnXMinus2()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 2, Compass.Dir8.West);
        foreach (var c in query)
            Assert.Equal(-2, c.X);
    }

    // ─────────────────────  Ординал NE — точные координаты угла  ─────────────────────

    /// <summary>
    /// NE-угол: 5 клеток (4·1+1) — North-грань (z=−1) + East-грань (x=+1), угол (1,−1) общий.
    /// half=1, pivot (0,0). Ожидаемые: (−1,−1),(0,−1),(1,−1),(1,0),(1,1).
    /// </summary>
    [Fact]
    public void NortheastCorner_Half1_FiveCells()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 1, Compass.Dir8.Northeast);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        Assert.Equal(new Vector2Int(-1, -1), cells[0]);
        Assert.Equal(new Vector2Int( 0, -1), cells[1]);
        Assert.Equal(new Vector2Int( 1, -1), cells[2]);
        Assert.Equal(new Vector2Int( 1,  0), cells[3]);
        Assert.Equal(new Vector2Int( 1,  1), cells[4]);
    }

    /// <summary>NE-угол с outline: каждая клетка сдвинута на (1,−1).</summary>
    [Fact]
    public void NortheastCorner_Outline_ShiftedByDir()
    {
        var query = new RectangleEdgeLineQuery(Vector2Int.Zero, half: 1, Compass.Dir8.Northeast, isOutline: true);
        var cells = Collect(query);
        var delta = Compass.Dir8.Northeast.Delta();

        Assert.Equal(5, cells.Count);
        Assert.Equal(new Vector2Int(-1, -1) + delta, cells[0]);
        Assert.Equal(new Vector2Int( 1,  1) + delta, cells[^1]);
    }

    // ─────────────────────  Изменённый pivot — сдвиг всей грани  ─────────────────────

    /// <summary>Грань смещается вместе с pivot: pivot (10,−5), East-грань на x=12.</summary>
    [Fact]
    public void EastEdge_NonZeroPivot_ShiftedColumn()
    {
        var query = new RectangleEdgeLineQuery(new Vector2Int(10, -5), half: 2, Compass.Dir8.East);
        var cells = Collect(query);

        Assert.Equal(5, cells.Count);
        foreach (var c in cells)
            Assert.Equal(12, c.X);   // 10 + half
    }

    // ─────────────────────  Helper  ─────────────────────

    /// <summary>Собрать клетки перечислителя в список (тесты — не горячий путь).</summary>
    private static List<Vector2Int> Collect(RectangleEdgeLineQuery query)
    {
        var list = new List<Vector2Int>(16);
        foreach (var cell in query)
            list.Add(cell);
        return list;
    }
}