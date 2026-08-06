namespace Verstack.Shared.Voxel;

/// <summary>
/// Генератор чанка: создаёт содержимое колонки по её координатам.
/// Не знает про ECS/сеть — чистая функция (chunkX, chunkZ) → ChunkColumn.
/// </summary>
public interface IChunkGenerator
{
    /// <summary>Сгенерировать колонку чанка. Вызывать можно off-thread: генератор
    /// не трогает разделяемое состояние, только создаёт новый ChunkColumn.</summary>
    ChunkColumn Generate(int chunkX, int chunkZ);
}