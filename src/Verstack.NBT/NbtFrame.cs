namespace Verstack.Nbt;

/// <summary>
/// Кадр стека вложенности <see cref="NbtWriter"/>: контекст текущего контейнера (Compound или List).
///
/// Writer ведёт стек кадров, чтобы при каждом <c>WriteXxx</c>/<c>Begin*</c> знать, писать ли имя тега и байт
/// типа: внутри Compound — каждый тег именованный (с type-байтом), внутри List — безымянный и без type-байта
/// (тип и количество уже объявлены в заголовке List). <see cref="ListRemaining"/> позволяет в DEBUG валидировать,
/// что число записанных элементов совпадает с заявленным в заголовке.
///
/// Поля публичны только потому, что тип должен быть виден caller'у для <c>stackalloc NbtFrame[N]</c> (буфер
/// кадров живёт на стеке у caller'а — основа GC-free дизайна writer'а). Трогать их вручную не нужно: writer
/// управляет кадрами сам. Для caller'а это opaque-буфер.
/// </summary>
public struct NbtFrame
{
    /// <summary>Тип контейнера кадра — Compound или List. Определяет, пишет ли writer имя тегу.</summary>
    public NbtTagType Container;

    /// <summary>
    /// Для List — ожидаемый тип элемента (из заголовка List). Для Compound — <see cref="NbtTagType.End"/>.
    /// В DEBUG сверяется с фактическим типом записываемого тега для раннего поимана рассогласования.
    /// </summary>
    public NbtTagType ExpectedListItem;

    /// <summary>
    /// Для List — сколько элементов ещё ждёт заголовок. Декрементируется на каждом безымянном <c>WriteXxx</c>/
    /// <c>Begin*</c>; <see cref="NbtWriter.EndList"/> валидирует, что остаток 0.
    /// </summary>
    public int ListRemaining;
}