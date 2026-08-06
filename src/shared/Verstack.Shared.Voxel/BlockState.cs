using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel;

/// <summary>
/// Идентификатор конкретного состояния блока в глобальной палитре (state id).
/// Это число кладётся в локальную палитру секции и отправляется клиенту.
/// Внимание: state id ≠ block id (см. <see cref="FlatBlockStates"/>).
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct BlockState(int id) : IEquatable<BlockState>
{
    /// <summary>State id в глобальной палитре minecraft:block.</summary>
    public readonly int Id = id;

    /// <summary>Воздух (state id = 0).</summary>
    public static BlockState Air => new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(BlockState other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is BlockState s && Equals(s);
    public override int GetHashCode() => Id;

    public static bool operator ==(BlockState a, BlockState b) => a.Id == b.Id;
    public static bool operator !=(BlockState a, BlockState b) => a.Id != b.Id;
}