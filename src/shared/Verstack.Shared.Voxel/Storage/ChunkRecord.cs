using Verstack.Shared.Voxel.Model;

namespace Verstack.Shared.Voxel.Storage;

/// <summary>
/// Запись о загруженной колонке в <see cref="ChunkBufferPool"/>: сами данные колонки
/// плюс счётчик удержаний (chunk tickets). Каждое <c>Acquire</c> инкрементирует
/// <see cref="RefCount"/>, парное <c>Release</c> — декрементирует; при достижении 0
/// запись покидает пул (eviction), а колонку подбирает сборщик мусора.
///
/// <para>Вынесена из <see cref="ChunkBufferPool"/> отдельно: здесь же будут жить метаданные
/// колонки по мере роста подсистемы — dirty-flag для resend, статус загрузки, timestamp
/// последнего доступа для LRU. Держать их во вложенной private-структуре пула значило бы
/// распухание самого пула, теперь же каждая ответственность на своём месте.</para>
/// </summary>
internal struct ChunkRecord
{
    /// <summary>Данные колонки чанка (24 секции + heightmaps).</summary>
    public ChunkColumn Column;

    /// <summary>Число активных удержаний (tickets). Колонка живёт в пуле, пока &gt; 0.</summary>
    public int RefCount;
}