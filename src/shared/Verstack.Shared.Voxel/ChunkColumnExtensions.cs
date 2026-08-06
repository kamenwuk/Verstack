namespace Verstack.Shared.Voxel;

/// <summary>
/// Массовые операции над колонкой чанка. Вынесены из <see cref="ChunkColumn"/>, чтобы
/// сам класс оставался чистой моделью данных (хранение + доступ к одному блоку), а все
/// удобные операции для генераторов жили отдельно и не засоряли контракт.
/// </summary>
public static class ChunkColumnExtensions
{
    /// <summary>
    /// Заполнить всю колонку одним биомом. Полезно для плоского мира (всё plains):
    /// один вызов вместо <c>SetBiome</c> по всем высотам.
    /// </summary>
    public static void FillBiome(this ChunkColumn column, int biomeId)
    {
        for (int i = 0; i < column.SectionCount; i++)
        {
            ref var section = ref column.GetSectionByIndex(i);
            for (int bx = 0; bx < 4; bx++)
            for (int by = 0; by < 4; by++)
            for (int bz = 0; bz < 4; bz++)
                section.SetBiome(bx * 4, by * 4, bz * 4, biomeId);
        }
    }
}