using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

/// <summary>
/// Paletted Container биомов секции: 64 записи (4³), палитра biome-id.
/// BPE-пороги по протоколу 776: 0 (single-valued) → 1–3 (indirect) → 7 (direct, TODO).
/// </summary>
public struct BiomeContainer(int initialBiomeId)
{
    private PackedPalette _palette = new(
        VoxelMetrics.SECTION_BIOME_COUNT, initialBiomeId, minIndirectBpe: 1, maxIndirectBpe: 3);

    public int BitsPerEntry => _palette.BitsPerEntry;
    public int PaletteSize => _palette.PaletteSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Get(int index) => _palette.Get(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, int biomeId) => _palette.Set(index, biomeId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetPaletteEntry(int paletteIndex) => _palette.GetPaletteEntry(paletteIndex);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int WriteTo(Span<byte> dest) => _palette.WriteTo(dest);
}