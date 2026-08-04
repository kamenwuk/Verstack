using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Verstack.Shared.Nbt;

/// <summary>
/// GC-free reader NBT (Named Binary Tag) из <c>ReadOnlySpan&lt;byte&gt;</c>. Stateful <c>ref struct</c>,
/// полный зеркало <see cref="NbtWriter"/>: те же контексты Compound/List через стек <see cref="NbtFrame"/>,
/// та же семантика имён и type-байтов.
///
/// Контекст чтения задаёт верхний кадр стека:
/// <list type="bullet">
/// <item>В <b>Compound</b> каждый тег именованный: <c>[type-байт][Short длина имени][modified-UTF-8 имя][payload]</c>.
/// Закрывается <c>0x00</c> (TAG_End) — читается в <see cref="ExitCompound"/>.</item>
/// <item>В <b>List</b> каждый элемент безымянный и без type-байта (тип и количество уже в заголовке List).
/// <see cref="ExitList"/> ничего не читает — длина уже была указана в заголовке.</item>
/// </list>
///
/// Два режима работы (см. тесты):
/// <list type="bullet">
/// <item><b>Sequental-core</b>: caller входит в контейнер, затем читает теги по порядку через
/// <see cref="ReadTagName"/> (возвращает zero-copy срез имени) + <see cref="ReadIntPayload"/> и т.п.</item>
/// <item><b>Lookup</b>: <see cref="TryReadInt(ReadOnlySpan{byte}, out int)"/> и т.п. — внутри Compound
/// сканируют теги вперёд до нужного имени. Scan только вперёд (без перемотки), как в реальных NBT-потоках.</item>
/// </list>
///
/// <b>GC-free.</b> Имена тегов возвращаются как <c>ReadOnlySpan&lt;byte&gt;</c> — zero-copy срез из буфера
/// reader'а, без декодирования (NBT-имена ASCII, mUTF-8 = ASCII byte-per-char). Lookup принимает
/// <c>ReadOnlySpan&lt;byte&gt;</c> (<c>"count"u8</c>) и сравнивает байт-в-байт, без аллокаций. Строковые
/// значения читаются в caller'ов <c>Span&lt;char&gt;</c> через декодер <see cref="ModifiedUtf8.Read"/>.
///
/// Корневой compound читается в networked-формате (по умолчанию, Configuration/Play 1.20.2+): байт типа
/// <c>0x0A</c> остаётся, поле имени пропускается. Disk-формат — параметром <c>networked: false</c>,
/// тогда после <c>0x0A</c> читается <c>Short=0</c> (пустое имя корня).
///
/// Структурная валидация (контекст, лишний Exit, рассогласование типов в List) — только в DEBUG
/// (как в <see cref="NbtWriter"/>). Чтение за пределами буфера (битый поток) → <see cref="EndOfStreamException"/>
/// всегда (как в Verstack.Network DataTypes).
/// </summary>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public ref struct NbtReader(ReadOnlySpan<byte> buffer, Span<NbtFrame> frames, bool networked = true)
{
    private readonly ReadOnlySpan<byte> _buffer = buffer;
    private readonly Span<NbtFrame> _frames = frames;
    private int _offset = 0;
    private int _depth = 0;

    /// <summary>Сколько байт прочитано из буфера.</summary>
    public int Read => _offset;

    /// <summary>Сколько байт осталось непрочитанным.</summary>
    public int Remaining => _buffer.Length - _offset;
    
    // ───────────────────────── Compound ─────────────────────────

    /// <summary>Входит в корневой compound: читает type-байт 0x0A (+ Short=0 для disk-формата).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterRootCompound()
    {
        NbtTagType type = ReadTagType();
#if DEBUG
        if (type != NbtTagType.Compound)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Корневой тег не Compound: {type}.");
#endif
        if (!networked)
        {
            short nameLen = ReadShortRaw();
#if DEBUG
            if (nameLen != 0)
                throw new InvalidOperationException(
                    $"[{nameof(NbtReader)}] Disk-root compound ожидает пустое имя (Short=0), получено {nameLen}.");
#endif
        }
        PushCompoundFrame();
    }

    /// <summary>Входит в именованный compound внутри Compound-контекста: читает [type+name] = 0x0A + имя.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterCompound(out string name)
    {
        // Caller уже сделал peek тега (ReadTagName) и знает, что это Compound.
        // Здесь — НЕ читаем type/name повторно: принимаем, что cursor стоит на payload.
        // Используется в связке с ReadTagName + EnterCompound, либо в lookup TryEnterCompound.
        // Эта перегрузка НЕ читает заголовок — для sequental-стиля с peek-ahead.
        // (Альтернатива: добавить EnterCompound(out name) с чтением заголовка — см. ниже).
        name = string.Empty; // placeholder; реальная читающая версия — в TryEnterCompound.
        PushCompoundFrame();
    }

    /// <summary>
    /// Входит в compound: ничего не читает, только push frame. Симметрично для двух случаев:
    /// <list type="bullet">
    /// <item>В Compound-контексте — после <see cref="ReadTagName"/> (type+name уже прочитаны, cursor стоит на payload).</item>
    /// <item>В List-контексте — как List-элемент (header List уже объявил тип).</item>
    /// </list>
    /// В первом случае caller сам сделал peek; во втором — <see cref="OnListItem"/> учтёт элемент List.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterCompound()
    {
        OnEnterContainer(NbtTagType.Compound);
        PushCompoundFrame();
    }

    /// <summary>
    /// Закрывает compound: читает и валидирует TAG_End (0x00), затем pop frame. Симметрия с
    /// <see cref="NbtWriter.EndCompound"/>, который пишет 0x00. End не потребляется в
    /// <see cref="ReadTagName"/> (там rollback) — единственное место потребления End — здесь.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCompound()
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ExitCompound на пустом стеке (лишний Exit).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ExitCompound в List-контексте (используйте ExitList).");
#endif
        NbtTagType end = ReadTagType();
#if DEBUG
        if (end != NbtTagType.End)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Ожидался TAG_End (0x00) в конце compound, получен {end}. " +
                $"Возможно, caller не прочитал все теги compound (остались непрочитанные теги).");
#endif
        PopFrame();
    }

    // ─────────────────────────  List  ─────────────────────────

    /// <summary>Входит в List как элемент List: читает только [elementType+count] (без type/name).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterList(out NbtTagType elementType, out int count)
    {
        OnEnterContainer(NbtTagType.List);
        elementType = ReadTagType();
        count = ReadIntRaw();
        PushListFrame(elementType, count);
    }

    /// <summary>Закрывает List: ничего не читает (длина уже в заголовке). В DEBUG проверяет остаток 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitList()
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ExitList на пустом стеке (лишний Exit).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ExitList в Compound-контексте (используйте ExitCompound).");
        if (frame.ListRemaining != 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] List закрыт с остатком: ожидалось ещё {frame.ListRemaining} элемент(ов).");
#endif
        PopFrame();
    }

    /// <summary>Осталось прочитать элементов в текущем List (для ручного обхода).</summary>
    public int ListRemaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if DEBUG
            if (_depth == 0 || _frames[_depth - 1].Container != NbtTagType.List)
                throw new InvalidOperationException(
                    $"[{nameof(NbtReader)}] ListRemaining вызван вне List-контекста.");
#endif
            return _frames[_depth - 1].ListRemaining;
        }
    }

    // ───────────────  Sequental: peek тега в Compound  ───────────────

    /// <summary>
    /// В Compound-контексте: читает [type-байт + имя], НЕ потребляя payload. Возвращает type=<see cref="NbtTagType.End"/>
    /// при достижении конца compound (TAG_End). При End делает rollback offset'а на 1 байт — TAG_End
    /// <b>не потребляется</b>, его прочитает <see cref="ExitCompound"/>. Это позволяет множественные
    /// lookup без побочных эффектов и устойчивость к отсутствующим полям (одно отсутствие не закрывает compound).
    ///
    /// Имя возвращается как <c>ReadOnlySpan&lt;byte&gt;</c> — zero-copy срез сырых modified-UTF-8 байт
    /// из буфера reader'а. Для NBT-имён (ASCII) это побайтово = кодовым точкам, и caller сравнивает
    /// с литералом через <c>utf8Name.SequenceEqual("count"u8)</c> без аллокаций. Срез живёт, пока жив
    /// буфер reader'а; не переживёт следующий <see cref="ReadTagName"/>/Read (но сравнение сразу после
    /// вызова — безопасно, срез фиксируется в caller'овой переменной до её использования).
    ///
    /// Caller решает, что с тегом делать: диспетчеризует по type (ReadIntPayload/ReadStringPayload/
    /// EnterList/...) или вызывает <see cref="SkipPayload(NbtTagType)"/> для пропуска.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> utf8Name)
    {
#if DEBUG
        ValidateCompoundContext();
#endif
        type = ReadTagType();
        if (type == NbtTagType.End)
        {
            // Rollback: End не потребляется. Его прочитает ExitCompound при закрытии compound.
            // Один байт назад — плата за robust lookup: false-результат TryReadXxx не закрывает compound,
            // и caller может продолжать lookup или корректно выйти через ExitCompound.
            _offset--;
            utf8Name = default;   // TAG_End не имеет имени.
            return;
        }
        utf8Name = ReadNameBytes();
    }

    // ───────────  Скаляры в List (без имени, без type-байта)  ───────────
    // Caller в List-контексте знает elementType из заголовка — type/name не читаются.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadByte()
    {
        OnListScalar(NbtTagType.Byte);
        return (sbyte)ReadByteRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadShort()
    {
        OnListScalar(NbtTagType.Short);
        return ReadShortRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt()
    {
        OnListScalar(NbtTagType.Int);
        return ReadIntRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong()
    {
        OnListScalar(NbtTagType.Long);
        return ReadLongRaw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        OnListScalar(NbtTagType.Float);
        return BitConverter.Int32BitsToSingle(ReadIntRaw());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        OnListScalar(NbtTagType.Double);
        return BitConverter.Int64BitsToDouble(ReadLongRaw());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadString(Span<char> destination, out int charsWritten)
    {
        OnListScalar(NbtTagType.String);
        ReadStringPayload(destination, out charsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        OnListScalar(NbtTagType.Byte);
        return ReadByteRaw() != 0;
    }

    // ───────────────  Sequental: потребление payload после peek  ───────────────
    // После ReadTagName caller вызывает эти методы для конкретного типа. ВАЖНО: они НЕ читают
    // type-байт и имя (уже прочитаны в ReadTagName), только payload.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadBytePayload() => (sbyte)ReadByteRaw();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadShortPayload() => ReadShortRaw();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadIntPayload() => ReadIntRaw();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLongPayload() => ReadLongRaw();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloatPayload() => BitConverter.Int32BitsToSingle(ReadIntRaw());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDoublePayload() => BitConverter.Int64BitsToDouble(ReadLongRaw());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBoolPayload() => ReadByteRaw() != 0;

    // ─────────────────────  Пропуск payload (для lookup/scan)  ─────────────────────

    /// <summary>
    /// Пропускает payload тега заданного <paramref name="type"/>. Используется lookup-хелперами
    /// (когда имя не совпало — пропустить значение и идти к следующему) и sequental-обходом с
    /// фильтрацией по имени. Для контейнеров (Compound/List) — рекурсивный пропуск всех потомков.
    /// ВАЖНО: type-байт и имя должны быть уже потреблены caller'ом (через ReadTagName).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipPayload(NbtTagType type)
    {
        switch (type)
        {
            case NbtTagType.Byte: _offset += 1; break;
            case NbtTagType.Short: _offset += 2; break;
            case NbtTagType.Int:
            case NbtTagType.Float: _offset += 4; break;
            case NbtTagType.Long:
            case NbtTagType.Double: _offset += 8; break;
            case NbtTagType.String:
                short len = ReadShortRaw();
                _offset += len;
                break;
            case NbtTagType.ByteArray:
                _offset += ReadIntRaw() * 1; break;
            case NbtTagType.IntArray:
                _offset += ReadIntRaw() * 4; break;
            case NbtTagType.LongArray:
                _offset += ReadIntRaw() * 8; break;
            case NbtTagType.List:
                SkipList();
                break;
            case NbtTagType.Compound:
                SkipCompound();
                break;
            default:
#if DEBUG
                throw new InvalidOperationException(
                    $"[{nameof(NbtReader)}] SkipPayload: неизвестный тип {type}.");
#else
                // В Release — End/unknown тип ничего не занимает.
                break;
#endif
        }
    }

    /// <summary>Пропускает List целиком: читает заголовок [elementType+count], затем пропускает count элементов.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipList()
    {
        NbtTagType elementType = ReadTagType();
        int count = ReadIntRaw();
        for (int i = 0; i < count; i++)
            SkipPayload(elementType);
    }

    /// <summary>Пропускает Compound целиком: читает теги до TAG_End (без push/pop кадров — обход вне стека).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipCompound()
    {
        while (true)
        {
            NbtTagType t = ReadTagType();
            if (t == NbtTagType.End) return;
            ReadNameBytes();        // пропустить имя (zero-copy, само имя не нужно)
            SkipPayload(t);
        }
    }

// ─────────────────────  Контекстные хелперы (internal — для расширений)  ─────────────────────

    /// <summary>
    /// Валидация и учёт <b>безымянного скаляра</b> в List-контексте в DEBUG. Строго List: в Compound
    /// бросает (caller должен использовать <c>ReadXxxPayload</c> после <see cref="ReadTagName"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("DEBUG")]
    internal void OnListScalar(NbtTagType type)
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Скаляр без имени вызван вне List-контекста (стек пуст).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Скаляр без имени вызван в Compound-контексте; используйте ReadTagName + ReadXxxPayload.");
        if (frame.ListRemaining <= 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] List переполнен: заявлено элементов меньше, чем прочитано.");
        if (frame.ExpectedListItem != type)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
        frame.ListRemaining--;
#endif
    }

    /// <summary>
    /// Валидация и учёт <b>входа в контейнер</b> (<see cref="EnterCompound"/>/<see cref="EnterList"/>).
    /// Универсальный: в Compound-контексте (после peek) — no-op, в List-контексте (как элемент List) —
    /// проверяет тип и декрементирует остаток.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("DEBUG")]
    internal void OnEnterContainer(NbtTagType type)
    {
#if DEBUG
        if (_depth == 0)
            return;   // EnterRootCompound вызывает push напрямую, минуя этот метод.
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            return;   // Compound-after-peek: name+type уже прочитаны в ReadTagName — корректно.
        if (frame.ListRemaining <= 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] List переполнен: заявлено элементов меньше, чем прочитано.");
        if (frame.ExpectedListItem != type)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
        frame.ListRemaining--;
#endif
    }

    /// <summary>
    /// Читает имя тега и возвращает <b>zero-copy срез</b> сырых modified-UTF-8 байт из буфера reader'а.
    /// Не декодирует: для NBT-имён (ASCII) сравнение с литералом идёт побайтово через SequenceEqual.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> ReadNameBytes()
    {
        short byteCount = ReadShortRaw();
#if DEBUG
        if (byteCount < 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Отрицательная длина имени: {byteCount}.");
#endif
        ReadOnlySpan<byte> name = _buffer[_offset..(_offset + byteCount)];
        _offset += byteCount;
        return name;
    }

    /// <summary>
    /// Читает payload строки (Short длина + modified-UTF-8 байты) и декодирует в caller'ов
    /// <paramref name="destination"/>. Используется в трёх местах: (1) sequental-чтение в Compound
    /// после peek (<c>ReadStringPayload</c>), (2) внутри <see cref="ReadString(Span{char}, out int)"/>
    /// для List-контекста, (3) внутри <see cref="TryReadString"/>. Одна реализация — три точки вызова.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadStringPayload(Span<char> destination, out int charsWritten)
    {
        short byteCount = ReadShortRaw();
#if DEBUG
        if (byteCount < 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Отрицательная длина строки: {byteCount}.");
#endif
        ModifiedUtf8.Read(_buffer[_offset..(_offset + byteCount)], destination, out charsWritten);
        _offset += byteCount;
    }

    // ─────────────────────  Raw-чтение BE-скаляров (internal — для расширений)  ─────────────────────

    /// <summary>Читает type-байт тега. Единственное место явного cast-а <c>byte → NbtTagType</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NbtTagType ReadTagType()
    {
        if ((uint)_offset >= (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении type-байта.");
        return (NbtTagType)_buffer[_offset++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte ReadByteRaw()
    {
        if ((uint)_offset >= (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении byte.");
        return _buffer[_offset++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal short ReadShortRaw()
    {
        if ((uint)(_offset + 2) > (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении short.");
        short v = BinaryPrimitives.ReadInt16BigEndian(_buffer[_offset..]);
        _offset += 2;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReadIntRaw()
    {
        if ((uint)(_offset + 4) > (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении int.");
        int v = BinaryPrimitives.ReadInt32BigEndian(_buffer[_offset..]);
        _offset += 4;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long ReadLongRaw()
    {
        if ((uint)(_offset + 8) > (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении long.");
        long v = BinaryPrimitives.ReadInt64BigEndian(_buffer[_offset..]);
        _offset += 8;
        return v;
    }

    /// <summary>Срез непрочитанных байт (для extensions, читающих массивы напрямую).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> ReadSpan(int count)
    {
        if ((uint)(_offset + count) > (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при чтении span ({count} байт).");
        ReadOnlySpan<byte> s = _buffer[_offset..(_offset + count)];
        _offset += count;
        return s;
    }

    /// <summary>Продвигает offset на count без копирования (для SkipPayload массивов).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(int count)
    {
        if ((uint)(_offset + count) > (uint)_buffer.Length)
            throw new EndOfStreamException($"[{nameof(NbtReader)}] Конец буфера при advance ({count} байт).");
        _offset += count;
    }

    // ─────────────────────────  Управление стеком  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushCompoundFrame() => PushFrame(NbtTagType.Compound, NbtTagType.End, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushListFrame(NbtTagType elementType, int count) => PushFrame(NbtTagType.List, elementType, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFrame(NbtTagType container, NbtTagType listItem, int remaining)
    {
#if DEBUG
        if (_depth >= _frames.Length)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Превышена глубина стека ({_frames.Length}). Увеличьте frames в конструкторе.");
#endif
        _frames[_depth++] = new NbtFrame
        {
            Container = container,
            ExpectedListItem = listItem,
            ListRemaining = remaining
        };
    }
        // ─────────────────────  Lookup по имени (в Compound-контексте)  ─────────────────────
    //
    // Scan-вперёд внутри текущего Compound до тега с заданным именем. Если имя нашлось и тип
    // совпал — читает payload, возвращает true. Если имя нашлось, но тип другой — бросает
    // (явный API-misuse: caller ожидал Int, а в потоке String под тем же именем). Если до конца
    // compound имя не встретилось — возвращает false, cursor стоит на TAG_End (НЕ потреблённом),
    // caller вызывает ExitCompound.
    //
    // Scan только вперёд — без перемотки. Реальные NBT-compound'ы имеют уникальные имена тегов,
    // так что повторный lookup того же имени после успешного чтения вернёт false. Если понадобится
    // перечитать — это новый EnterCompound на свежем reader'е.

    /// <summary>
    /// Ищет <see cref="NbtTagType.Byte"/> с заданным <paramref name="nameUtf8"/> в текущем Compound,
    /// пропуская несовпадающие теги. Не найдено → false; нашлось, но тип другой → исключение.
    /// Имя передаётся как modified-UTF-8 байты (для ASCII это <c>"name"u8</c>) — сравнение побайтовое.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadByte(ReadOnlySpan<byte> nameUtf8, out sbyte value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Byte)) { value = (sbyte)ReadByteRaw(); return true; }
        value = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadShort(ReadOnlySpan<byte> nameUtf8, out short value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Short)) { value = ReadShortRaw(); return true; }
        value = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadInt(ReadOnlySpan<byte> nameUtf8, out int value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Int)) { value = ReadIntRaw(); return true; }
        value = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLong(ReadOnlySpan<byte> nameUtf8, out long value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Long)) { value = ReadLongRaw(); return true; }
        value = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadFloat(ReadOnlySpan<byte> nameUtf8, out float value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Float)) { value = BitConverter.Int32BitsToSingle(ReadIntRaw()); return true; }
        value = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadDouble(ReadOnlySpan<byte> nameUtf8, out double value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Double)) { value = BitConverter.Int64BitsToDouble(ReadLongRaw()); return true; }
        value = 0; return false;
    }

    /// <summary>
    /// Ищет <see cref="NbtTagType.String"/> с именем <paramref name="nameUtf8"/> и декодирует значение
    /// в caller'ов <paramref name="destination"/> (zero-alloc). Симметрия со скалярными lookup'ами.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadString(ReadOnlySpan<byte> nameUtf8, Span<char> destination, out int charsWritten)
    {
        if (TrySeekName(nameUtf8, NbtTagType.String)) { ReadStringPayload(destination, out charsWritten); return true; }
        charsWritten = 0; return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadBool(ReadOnlySpan<byte> nameUtf8, out bool value)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Byte)) { value = ReadByteRaw() != 0; return true; }
        value = false; return false;
    }

    /// <summary>
    /// Ищет вложенный Compound с заданным именем и входит в него (push frame). Caller после
    /// успешного true обязан прочитать содержимое и вызвать <see cref="ExitCompound"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterCompound(ReadOnlySpan<byte> nameUtf8)
    {
        if (TrySeekName(nameUtf8, NbtTagType.Compound)) { PushCompoundFrame(); return true; }
        return false;
    }

    /// <summary>
    /// Ищет List с заданным именем и входит в него (push frame). Возвращает elementType/count
    /// через out-параметры. Caller обязан прочитать count элементов и вызвать <see cref="ExitList"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterList(ReadOnlySpan<byte> nameUtf8, out NbtTagType elementType, out int count)
    {
        if (TrySeekName(nameUtf8, NbtTagType.List))
        {
            elementType = ReadTagType();
            count = ReadIntRaw();
            PushListFrame(elementType, count);
            return true;
        }
        elementType = default; count = 0; return false;
    }

    /// <summary>
    /// Ядро lookup: scan вперёд до тега с именем <paramref name="nameUtf8"/> и типом <paramref name="expected"/>.
    /// Сравнение имён — побайтовое (<see cref="ReadOnlySpan{T}"/>.<see cref="MemoryExtensions.SequenceEqual{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>),
    /// без декодирования: NBT-имена ASCII, mUTF-8 = ASCII byte-per-char. Не совпало имя →
    /// <see cref="SkipPayload(NbtTagType)"/> и дальше. Совпало имя, но не тип → исключение (явный API-misuse).
    /// Достигли конца compound → false, при этом <see cref="ReadTagName"/> сделал rollback: TAG_End не
    /// потреблён, cursor стоит на нём — caller вызывает <see cref="ExitCompound"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TrySeekName(ReadOnlySpan<byte> nameUtf8, NbtTagType expected)
    {
#if DEBUG
        ValidateCompoundContextForLookup();
#endif
        while (true)
        {
            ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> currentName);
            if (type == NbtTagType.End)
                return false;   // ReadTagName уже сделал rollback — cursor на End, не потреблён.
            if (currentName.SequenceEqual(nameUtf8))
            {
#if DEBUG
                if (type != expected)
                    throw new InvalidOperationException(
                        $"[{nameof(NbtReader)}] Тег найден, но тип {type} ≠ ожидаемому {expected}.");
#endif
                return true;
            }
            SkipPayload(type);   // не наш тег — пропускаем payload и идём к следующему.
        }
    }
    
    /// <summary>
    /// Пропускает все оставшиеся теги в текущем Compound до (но не включая) TAG_End. End НЕ
    /// потребляется — caller зовёт <see cref="ExitCompound"/> для закрытия. Удобен после lookup'ов:
    /// нужные поля прочитаны, остальные не интересуют — пропускаем и закрываем.
    ///
    /// В DEBUG проверяет Compound-контекст. В List использовать нельзя (там нет именованных тегов).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipRemaining()
    {
#if DEBUG
        ValidateCompoundContext();
#endif
        while (true)
        {
            ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> _);
            if (type == NbtTagType.End)
                return;   // ReadTagName сделал rollback — End не потреблён, его прочитает ExitCompound.
            SkipPayload(type);
        }
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCompoundContextForLookup()
    {
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Lookup вызван до EnterRootCompound (стек пуст).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] Lookup в List-контексте; имена тегов есть только в Compound.");
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopFrame()
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] PopFrame на пустом стеке.");
#endif
        _depth--;
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCompoundContext()
    {
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ReadTagName вызван до EnterRootCompound (стек пуст).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException(
                $"[{nameof(NbtReader)}] ReadTagName в List-контексте; используйте безымянные перегрузки.");
    }
#endif
}