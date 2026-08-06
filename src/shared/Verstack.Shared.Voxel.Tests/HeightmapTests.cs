namespace Verstack.Shared.Voxel.Tests;

public class HeightmapTests
{
    [Fact]
    public void Heightmap_SetGet_RoundtripWorldY()
    {
        var h = new Heightmap();
        h.SetTopY(3, 7, 100);
        Assert.Equal(100, h.GetTopY(3, 7));
    }

    [Fact]
    public void Heightmap_Empty_RoundTripsAsEmpty()
    {
        var h = new Heightmap();
        h.Fill(Heightmap.EMPTY);
        Assert.Equal(Heightmap.EMPTY, h.GetTopY(0, 0));
        Assert.Equal(Heightmap.EMPTY, h.GetTopY(15, 15));
    }

    [Fact]
    public void Heightmap_Fill_RoundTripsAllColumns()
    {
        var h = new Heightmap();
        h.Fill(64);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
            Assert.Equal(64, h.GetTopY(x, z));
    }
}