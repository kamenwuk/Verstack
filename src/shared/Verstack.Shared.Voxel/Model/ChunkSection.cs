using System.Runtime.CompilerServices;
using Verstack.Shared.Voxel.Encoding;

namespace Verstack.Shared.Voxel.Model;

/// <summary>
/// Секция чанка: 16³ блоков + биомы. Единица вертикального дробления колонки.
///
/// <para>Хранит два <see cref="PackedPalette"/>-а — состояний блоков (4096) и биомов (64) —
/// и два счётчика для wire-формата: <see cref="BlockCount"/> (не-air блоков) и
/// <see cref="FluidCount"/> (жидкостей). <see cref="BlockCount"/> обновляется инкрементально
/// в <see cref="SetBlock"/>: клиент не рендерит секцию при BlockCount=0, даже если блоки есть.</para>
/// </summary>
public struct ChunkSection
{
    private BlockStateContainer _blocks;
    private BiomeContainer _biomes;
    private short _blockCount;
    private short _fluidCount;

    /// <summary>Не-air блоков (всё, кроме air/cave air/void air).</summary>
    public short BlockCount => _blockCount;

    /// <summary>Жидкости (water/lava + waterlogged). Для плоского мира всегда 0; TODO: учёт.</summary>
    public short FluidCount => _fluidCount;

    public readonly BlockStateContainer Blocks => _blocks;
    public readonly BiomeContainer Biomes => _biomes;

    /// <summary>Создать однородную секцию (single-valued) — воздух/биом по умолчанию.</summary>
    public ChunkSection(int initialStateId, int initialBiomeId)
    {
        _blocks = new BlockStateContainer(initialStateId);
        _biomes = new BiomeContainer(initialBiomeId);
        _blockCount = initialStateId == FlatBlockStates.AIR
            ? (short)0
            : (short)VoxelMetrics.SECTION_BLOCK_COUNT;
        _fluidCount = 0;
    }

    /// <summary>State id блока по локальным координатам секции [0..16).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetBlock(int localX, int localY, int localZ)
        => _blocks.Get(VoxelMetrics.BlockIndex(localX, localY, localZ));

    /// <summary>Записать блок. Инкрементально обновляет <see cref="BlockCount"/>.</summary>
    public void SetBlock(int localX, int localY, int localZ, int stateId)
    {
        var index = VoxelMetrics.BlockIndex(localX, localY, localZ);
        var old = _blocks.Get(index);
        if (old == stateId)
            return;

        var wasAir = old == FlatBlockStates.AIR;
        var nowAir = stateId == FlatBlockStates.AIR;
        if (wasAir && !nowAir) _blockCount++;
        else if (!wasAir && nowAir) _blockCount--;

        _blocks.Set(index, stateId);
    }

    /// <summary>Biome id по локальным координатам секции.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetBiome(int localX, int localY, int localZ)
        => _biomes.Get(VoxelMetrics.BiomeIndex(localX, localY, localZ));

    /// <summary>Записать биом в ячейку 4×4×4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBiome(int localX, int localY, int localZ, int biomeId)
        => _biomes.Set(VoxelMetrics.BiomeIndex(localX, localY, localZ), biomeId);
    
    /// <summary>
    /// Сериализовать секцию в Span (BlockCount + FluidCount + 2 PalettedContainer).
    /// Возвращает число записанных байт.
    /// </summary>
    public readonly int WriteTo(Span<byte> dest)
    {
        var offset = 0;
        System.Buffers.Binary.BinaryPrimitives.WriteInt16BigEndian(dest[offset..], _blockCount);
        offset += 2;
        System.Buffers.Binary.BinaryPrimitives.WriteInt16BigEndian(dest[offset..], _fluidCount);
        offset += 2;
        offset += _blocks.WriteTo(dest[offset..]);
        offset += _biomes.WriteTo(dest[offset..]);
        return offset;
    }
}