namespace Verstack.Shared.Voxel.Tests;

public class ChunkSectionTests
{
    [Fact]
    public void ChunkSection_SingleValuedAir_BlockCountZero()
    {
        var s = new ChunkSection(FlatBlockStates.AIR, FlatBlockStates.BIOME_PLAINS);
        Assert.Equal(0, s.BlockCount);
        Assert.Equal(FlatBlockStates.AIR, s.GetBlock(0, 0, 0));
    }

    [Fact]
    public void ChunkSection_SingleValuedStone_BlockCountFull()
    {
        var s = new ChunkSection(FlatBlockStates.STONE, FlatBlockStates.BIOME_PLAINS);
        Assert.Equal(VoxelMetrics.SECTION_BLOCK_COUNT, s.BlockCount);
    }

    [Fact]
    public void ChunkSection_SetBlock_IncrementsAndDecrementsCount()
    {
        var s = new ChunkSection(FlatBlockStates.AIR, FlatBlockStates.BIOME_PLAINS);

        s.SetBlock(0, 0, 0, FlatBlockStates.STONE);
        Assert.Equal(1, s.BlockCount);

        s.SetBlock(0, 0, 0, FlatBlockStates.AIR);
        Assert.Equal(0, s.BlockCount);

        // тот же блок, что уже стоит — count не меняется
        s.SetBlock(5, 5, 5, FlatBlockStates.DIRT);
        s.SetBlock(5, 5, 5, FlatBlockStates.DIRT);
        Assert.Equal(1, s.BlockCount);
    }
}