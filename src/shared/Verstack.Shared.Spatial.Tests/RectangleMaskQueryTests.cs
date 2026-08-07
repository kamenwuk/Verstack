using Verstack.Shared.Maths;
using Verstack.Shared.Spatial.Queries;

namespace Verstack.Shared.Spatial.Tests;

/// <summary>
/// Тесты <see cref="RectangleMaskQuery"/>: размер квадрата (2·half+1)², границы bbox,
/// уникальность клеток, сдвиг вместе с pivot.
/// </summary>
public class RectangleMaskQueryTests
{
    // ─────────────────────  Размер — (2·half+1)²  ─────────────────────

    [Theory]
    [InlineData(0, 1)]    // 1×1
    [InlineData(1, 9)]    // 3×3
    [InlineData(2, 25)]   // 5×5
    [InlineData(5, 121)]  // 11×11
    public void Count_MatchesBoxArea(int half, int expected)
    {
        var query = new RectangleMaskQuery(Vector2Int.Zero, half);
        var count = 0;
        foreach (var _ in query)
            count++;
        Assert.Equal(expected, count);
    }

    // ─────────────────────  Границы bbox  ─────────────────────

    /// <summary>half=2, pivot (0,0): все клетки в диапазоне [−2,+2] по обеим осям.</summary>
    [Fact]
    public void Half2_PivotZero_AllCellsInRange()
    {
        var query = new RectangleMaskQuery(Vector2Int.Zero, half: 2);
        foreach (var c in query)
        {
            Assert.InRange(c.X, -2, 2);
            Assert.InRange(c.Y, -2, 2);
        }
    }

    /// <summary>Углы квадрата присутствуют: (−2,−2) и (+2,+2) при half=2.</summary>
    [Fact]
    public void Half2_CornersPresent()
    {
        var cells = Collect(new RectangleMaskQuery(Vector2Int.Zero, half: 2));
        Assert.Contains(new Vector2Int(-2, -2), cells);
        Assert.Contains(new Vector2Int( 2,  2), cells);
        Assert.Contains(new Vector2Int(-2,  2), cells);
        Assert.Contains(new Vector2Int( 2, -2), cells);
    }

    // ─────────────────────  Уникальность — нет дублей  ─────────────────────

    /// <summary>Каждая клетка встречается ровно один раз.</summary>
    [Fact]
    public void Half2_NoDuplicates()
    {
        var cells = Collect(new RectangleMaskQuery(Vector2Int.Zero, half: 2));
        var distinct = cells.Distinct().Count();
        Assert.Equal(cells.Count, distinct);
    }

    // ─────────────────────  Сдвиг с pivot  ─────────────────────

    /// <summary>pivot (10,−5), half=1: 9 клеток в диапазоне [9,11]×[−6,−4].</summary>
    [Fact]
    public void NonZeroPivot_ShiftedBox()
    {
        var query = new RectangleMaskQuery(new Vector2Int(10, -5), half: 1);
        var cells = Collect(query);

        Assert.Equal(9, cells.Count);
        foreach (var c in cells)
        {
            Assert.InRange(c.X, 9, 11);
            Assert.InRange(c.Y, -6, -4);
        }
    }

    // ─────────────────────  Helper  ─────────────────────

    private static List<Vector2Int> Collect(RectangleMaskQuery query)
    {
        var list = new List<Vector2Int>(64);
        foreach (var cell in query)
            list.Add(cell);
        return list;
    }
}