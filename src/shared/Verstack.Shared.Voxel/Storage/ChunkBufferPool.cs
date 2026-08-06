using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Verstack.Shared.Voxel.Generation;
using Verstack.Shared.Voxel.Model;

namespace Verstack.Shared.Voxel.Storage;

/// <summary>
/// Buffer pool чанков: in-memory пул колонок с подсчётом ссылок и eviction при
/// <c>refCount == 0</c>. По принципу — buffer pool СУБД (Postgres/InnoDB): страницы
/// (у нас — колонки) удерживаются в памяти, пока есть «пины»; при отпускании последнего
/// страница выгружается.
///
/// <para>Каждая колонка существует в единственном экземпляре: арендаторы получают одну и ту
/// же ссылку через <see cref="Acquire"/> и шарят её — нет дублирования данных. Несколько
/// игроков в одной области держат общую колонку.</para>
///
/// <para>Не потокобезопасен: как и весь ECS-слой Leopotam, доступ синхронизируется системами
/// (один поток тик-лупа). Регистрируется как сервис через <c>ServerComposer.AddService</c> и
/// инъектится в системы через <c>[DI]</c>.</para>
///
/// <para>Персистентность (region-файлы) — отдельный будущий слой <b>под</b> пулом: загрузка
/// идёт <c>region → pool</c>, выгрузка при eviction может (позже) стекать в region. Сейчас
/// генератор создаёт колонку напрямую.</para>
///
/// <para>Ключ координат: <c>((long)cx &lt;&lt; 32) | (uint)cz</c> — укладывает (X, Z) в одно
/// <c>long</c>-значение, корректно для отрицательных координат.</para>
/// </summary>
public sealed class ChunkBufferPool
{
    private readonly IChunkGenerator _generator;
    private readonly Dictionary<long, ChunkRecord> _records = new();

    /// <summary>
    /// Создать пул с заданным генератором чанков.
    /// </summary>
    public ChunkBufferPool(IChunkGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>
    /// Создать пул с генератором по умолчанию (<see cref="FlatGenerator"/> — плоский мир).
    /// </summary>
    public ChunkBufferPool() : this(new FlatGenerator()) { }

    /// <summary>
    /// Арендовать (пин) колонку чанка: возвращает существующую (increment <c>refCount</c>)
    /// либо генерирует новую (<c>refCount = 1</c>). Каждый <see cref="Acquire"/> должен быть
    /// парным с <see cref="Release"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkColumn Acquire(int cx, int cz)
    {
        var key = PackKey(cx, cz);
        ref var record = ref CollectionsMarshal.GetValueRefOrNullRef(_records, key);
        if (!Unsafe.IsNullRef(ref record))
        {
            record.RefCount++;
            return record.Column;
        }

        var column = _generator.Generate(cx, cz);
        _records[key] = new ChunkRecord { Column = column, RefCount = 1 };
        return column;
    }

    /// <summary>
    /// Освободить (анпин) аренду колонки: decrement <c>refCount</c>, при достижении 0 —
    /// eviction (запись удаляется из пула; сборщик мусора вернёт память). Освобождение
    /// чанка, который не был загружен, — no-op (легальный double-release).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(int cx, int cz)
    {
        ref var record = ref CollectionsMarshal.GetValueRefOrNullRef(_records, PackKey(cx, cz));
        if (Unsafe.IsNullRef(ref record))
            return;

        record.RefCount--;
        if (record.RefCount <= 0)
            _records.Remove(PackKey(cx, cz));
    }

    /// <summary>
    /// Получить колонку без изменения <c>refCount</c> — для соседних lookups (чтение блоков,
    /// проверка поверхности и т.п.). Не удерживает колонку: она может быть выгружена сразу
    /// после возврата, если нет других арендаторов. Возвращает <c>false</c>, если колонка
    /// не в пуле.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(int cx, int cz, out ChunkColumn? column)
    {
        ref var record = ref CollectionsMarshal.GetValueRefOrNullRef(_records, PackKey(cx, cz));
        if (!Unsafe.IsNullRef(ref record))
        {
            column = record.Column;
            return true;
        }
        column = null;
        return false;
    }

    /// <summary>Количество колонок в пуле (диагностика/дебаг).</summary>
    public int Count => _records.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackKey(int cx, int cz) => ((long)cx << 32) | (uint)cz;
}