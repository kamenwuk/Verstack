using Verstack.Shared.Voxel.Generation;

namespace Verstack.Shared.Voxel.Tests;

public class FlatGeneratorTests
{
    [Fact]
    public void FlatGenerator_Surface_IsGrassAt64()
    {
        var gen = new FlatGenerator();
        var c = gen.Generate(0, 0);

        Assert.Equal(FlatBlockStates.GRASS_BLOCK, c.GetBlock(0, FlatGenerator.SURFACE_Y, 0));
        Assert.Equal(FlatBlockStates.AIR, c.GetBlock(0, FlatGenerator.SURFACE_Y + 1, 0));
    }

    [Fact]
    public void FlatGenerator_Layers_InOrder()
    {
        var gen = new FlatGenerator();
        var c = gen.Generate(5, -3); // координата чанка не влияет на содержимое

        Assert.Equal(FlatBlockStates.BEDROCK, c.GetBlock(8, -64, 8));
        Assert.Equal(FlatBlockStates.STONE, c.GetBlock(8, 0, 8));
        Assert.Equal(FlatBlockStates.STONE, c.GetBlock(8, 61, 8));
        Assert.Equal(FlatBlockStates.DIRT, c.GetBlock(8, 62, 8));
        Assert.Equal(FlatBlockStates.DIRT, c.GetBlock(8, 63, 8));
        Assert.Equal(FlatBlockStates.GRASS_BLOCK, c.GetBlock(8, 64, 8));
    }

    [Fact]
    public void FlatGenerator_Heightmap_IsSurfaceY()
    {
        var gen = new FlatGenerator();
        var c = gen.Generate(0, 0);

        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            Assert.Equal(FlatGenerator.SURFACE_Y, c.MotionBlocking.GetTopY(x, z));
            Assert.Equal(FlatGenerator.SURFACE_Y, c.WorldSurface.GetTopY(x, z));
        }
    }

    [Fact]
    public void FlatGenerator_IdenticalAcrossChunks()
    {
        var gen = new FlatGenerator();
        var a = gen.Generate(0, 0);
        var b = gen.Generate(100, -50);

        // Плоский мир — все чанки идентичны.
        Assert.Equal(a.GetBlock(3, 64, 7), b.GetBlock(3, 64, 7));
        Assert.Equal(a.GetBlock(3, -64, 7), b.GetBlock(3, -64, 7));
    }
}