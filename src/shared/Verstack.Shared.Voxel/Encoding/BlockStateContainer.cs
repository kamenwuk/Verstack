using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel.Encoding;

/// <summary>
/// Paletted Container состояний блоков секции: 4096 записей (16³), палитра state-id.
/// BPE-пороги по протоколу 776: 0 (single-valued) → 4–8 (indirect) → 15 (direct, TODO).
/// </summary>
public struct BlockStateContainer(int initialStateId)
{
    private PackedPalette _palette = new(
        VoxelMetrics.SECTION_BLOCK_COUNT, initialStateId, minIndirectBpe: 4, maxIndirectBpe: 8);

    public int BitsPerEntry => _palette.BitsPerEntry;
    public int PaletteSize => _palette.PaletteSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Get(int index) => _palette.Get(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, int stateId) => _palette.Set(index, stateId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetPaletteEntry(int paletteIndex) => _palette.GetPaletteEntry(paletteIndex);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int WriteTo(Span<byte> dest) => _palette.WriteTo(dest);
}