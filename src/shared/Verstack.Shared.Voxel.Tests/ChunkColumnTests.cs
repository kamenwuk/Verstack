using Verstack.Shared.Voxel.Model;

namespace Verstack.Shared.Voxel.Tests;

public class ChunkColumnTests
{
    [Fact]
    public void ChunkColumn_SetGetBlock_Roundtrip()
    {
        var c = new ChunkColumn();
        c.SetBlock(0, 64, 0, FlatBlockStates.GRASS_BLOCK);
        Assert.Equal(FlatBlockStates.GRASS_BLOCK, c.GetBlock(0, 64, 0));
    }

    [Fact]
    public void ChunkColumn_SetBlock_UpdatesHeightmap()
    {
        var c = new ChunkColumn();
        c.SetBlock(3, 70, 5, FlatBlockStates.STONE);
        Assert.Equal(70, c.MotionBlocking.GetTopY(3, 5));
        Assert.Equal(70, c.WorldSurface.GetTopY(3, 5));
    }

    [Fact]
    public void ChunkColumn_LowerY_DoesNotOverrideHigher()
    {
        var c = new ChunkColumn();
        c.SetBlock(0, 80, 0, FlatBlockStates.STONE);
        c.SetBlock(0, 50, 0, FlatBlockStates.DIRT); // ниже — не перетирает top=80
        Assert.Equal(80, c.MotionBlocking.GetTopY(0, 0));
    }
}