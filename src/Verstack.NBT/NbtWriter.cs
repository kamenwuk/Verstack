using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Verstack.Nbt;

/// <summary>
/// GC-free writer NBT (Named Binary Tag) прямо в <c>Span&lt;byte&gt;</c>. Stateful <c>ref struct</c>:
/// помнит контекст вложенности (Compound/List) через стек <see cref="NbtFrame"/> и сам решает, писать ли
/// имя тегу и байт типа.
///
/// Контекст записи задаёт верхний кадр стека:
/// <list type="bullet">
/// <item>В <b>Compound</b> каждый тег именованный: <c>[type-байт][Short длина имени][modified-UTF-8 имя][payload]</c>.
/// Закрывается <see cref="EndCompound"/> — пишется <c>0x00</c> (TAG_End).</item>
/// <item>В <b>List</b> каждый элемент безымянный и без type-байта (тип и количество уже в заголовке List).
/// <see cref="EndList"/> ничего не пишет — длина уже указана в заголовке.</item>
/// </list>
///
/// Корневой compound пишется в networked-формате (по умолчанию, Configuration/Play 1.20.2+): байт типа
/// <c>0x0A</c> остаётся, поле имени пропускается. Disk-формат (для тестов/свёрки) — параметром
/// <c>networked: false</c>, тогда после <c>0x0A</c> пишется <c>Short=0</c> (пустое имя корня).
///
/// Массивы (ByteArray/IntArray/LongArray) вынесены в <c>NbtWriterArrayExtensions</c>. Структурная валидация
/// (контекст, переполнение буфера/стека, длина строки, рассогласование типов в List) — только в DEBUG.
/// </summary>
public ref struct NbtWriter
{
    private const int MAX_STRING_LENGTH = 32767;

    private readonly Span<byte> _buffer;
    private readonly Span<NbtFrame> _frames;
    private readonly bool _networked;
    private int _offset;
    private int _depth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NbtWriter(Span<byte> buffer, Span<NbtFrame> frames, bool networked = true)
    {
        _buffer = buffer;
        _frames = frames;
        _networked = networked;
        _offset = 0;
        _depth = 0;
    }

    public int Written => _offset;

    public ReadOnlySpan<byte> WrittenSpan => _buffer[.._offset];

    // ───────────────────────── Compound ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginRootCompound()
    {
        WriteTagType(NbtTagType.Compound);
        if (!_networked)
            WriteShortRaw(0);   // disk-root: пустое имя корня (Short длина = 0).
        PushCompoundFrame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginCompound(string name)
    {
        WriteNameAndType(NbtTagType.Compound, name);
        PushCompoundFrame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginCompound()
    {
        OnListItem(NbtTagType.Compound);
        PushCompoundFrame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndCompound()
    {
        WriteTagType(NbtTagType.End);
        PopFrame();
    }

    // ─────────────────────────  List  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginList(string name, NbtTagType elementType, int count)
    {
        WriteNameAndType(NbtTagType.List, name);
        WriteListHeader(elementType, count);
        PushListFrame(elementType, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginList(NbtTagType elementType, int count)
    {
        OnListItem(NbtTagType.List);
        WriteListHeader(elementType, count);
        PushListFrame(elementType, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndList()
    {
#if DEBUG
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] EndList вызван вне List-контекста (текущий контейнер: {frame.Container}).");
        if (frame.ListRemaining != 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] List закрыт с остатком: ожидалось ещё {frame.ListRemaining} элемент(ов).");
#endif
        PopFrame();
    }

    // ───────────────  Скаляры в Compound (с именем)  ───────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(string name, sbyte value)
    {
        WriteNameAndType(NbtTagType.Byte, name);
        WriteByteRaw((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteShort(string name, short value)
    {
        WriteNameAndType(NbtTagType.Short, name);
        WriteShortRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(string name, int value)
    {
        WriteNameAndType(NbtTagType.Int, name);
        WriteIntRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(string name, long value)
    {
        WriteNameAndType(NbtTagType.Long, name);
        WriteLongRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(string name, float value)
    {
        WriteNameAndType(NbtTagType.Float, name);
        WriteIntRaw(BitConverter.SingleToInt32Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(string name, double value)
    {
        WriteNameAndType(NbtTagType.Double, name);
        WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string name, string value)
    {
        WriteNameAndType(NbtTagType.String, name);
        WriteStringPayload(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(string name, bool value)
    {
        WriteNameAndType(NbtTagType.Byte, name);
        WriteByteRaw(value ? (byte)1 : (byte)0);
    }

    // ───────────  Скаляры в List (без имени, без type-байта)  ───────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(sbyte value)
    {
        OnListItem(NbtTagType.Byte);
        WriteByteRaw((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteShort(short value)
    {
        OnListItem(NbtTagType.Short);
        WriteShortRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value)
    {
        OnListItem(NbtTagType.Int);
        WriteIntRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value)
    {
        OnListItem(NbtTagType.Long);
        WriteLongRaw(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        OnListItem(NbtTagType.Float);
        WriteIntRaw(BitConverter.SingleToInt32Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        OnListItem(NbtTagType.Double);
        WriteLongRaw(BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value)
    {
        OnListItem(NbtTagType.String);
        WriteStringPayload(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value)
    {
        OnListItem(NbtTagType.Byte);
        WriteByteRaw(value ? (byte)1 : (byte)0);
    }

    // ─────────────────────  Контекстные хелперы (internal — для расширений)  ─────────────────────

    /// <summary>
    /// Валидация и учёт List-элемента в DEBUG. В Release вырождается в no-op (<c>[Conditional]</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("DEBUG")]
    internal void OnListItem(NbtTagType type)
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Скаляр без имени вызван вне List-контекста (стек пуст).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Скаляр без имени вызван в Compound-контексте; используйте перегрузку с name.");
        if (frame.ListRemaining <= 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] List переполнен: заявлено элементов меньше, чем записано.");
        if (frame.ExpectedListItem != type)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
        frame.ListRemaining--;
#endif
    }

    /// <summary>Записывает type-байт + имя тега (для именованного тега в Compound).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteNameAndType(NbtTagType type, string name)
    {
#if DEBUG
        ValidateCompoundContext(type);
#endif
        WriteTagType(type);
        WriteName(name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteListHeader(NbtTagType elementType, int count)
    {
#if DEBUG
        if (count < 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Отрицательная длина List: {count}.");
#endif
        WriteTagType(elementType);
        WriteIntRaw(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteName(string name)
    {
        int byteCount = ModifiedUtf8.GetByteCount(name);
#if DEBUG
        if (byteCount > MAX_STRING_LENGTH)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Имя тега слишком длинное: {byteCount} байт modified-UTF-8 (max {MAX_STRING_LENGTH}).");
#endif
        WriteShortRaw((short)byteCount);
        ModifiedUtf8.Write(name, _buffer[_offset..]);
        _offset += byteCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteStringPayload(string value)
    {
        int byteCount = ModifiedUtf8.GetByteCount(value);
#if DEBUG
        if (byteCount > MAX_STRING_LENGTH)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] TAG_String слишком длинная: {byteCount} байт modified-UTF-8 (max {MAX_STRING_LENGTH}).");
#endif
        WriteShortRaw((short)byteCount);
        ModifiedUtf8.Write(value, _buffer[_offset..]);
        _offset += byteCount;
    }

    // ─────────────────────  Raw-запись BE-скаляров (internal — для расширений)  ─────────────────────

    /// <summary>Записывает type-байт тега. Единственное место явного cast-а <c>NbtTagType → byte</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteTagType(NbtTagType type) => WriteByteRaw((byte)type);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteByteRaw(byte value)
    {
        _buffer[_offset] = value;
        _offset += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteShortRaw(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_buffer[_offset..], value);
        _offset += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteIntRaw(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_buffer[_offset..], value);
        _offset += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteLongRaw(long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(_buffer[_offset..], value);
        _offset += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteSpan(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_buffer[_offset..]);
        _offset += value.Length;
    }

    // ─────────────────────────  Управление стеком  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushCompoundFrame()
    {
        PushFrame(NbtTagType.Compound, NbtTagType.End, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushListFrame(NbtTagType elementType, int count)
    {
        PushFrame(NbtTagType.List, elementType, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFrame(NbtTagType container, NbtTagType listItem, int remaining)
    {
#if DEBUG
        if (_depth >= _frames.Length)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Превышена глубина стека ({_frames.Length}). Увеличьте frames в конструкторе.");
#endif
        _frames[_depth++] = new NbtFrame
        {
            Container = container,
            ExpectedListItem = listItem,
            ListRemaining = remaining
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopFrame()
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] PopFrame на пустом стеке (лишний End* вызов).");
#endif
        _depth--;
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCompoundContext(NbtTagType type)
    {
        if (_depth == 0)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Именованный тег {type} записан до BeginRootCompound/BeginCompound.");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException(
                $"[{nameof(NbtWriter)}] Именованный тег {type} записан в List-контексте; используйте безымянную перегрузку.");
    }
#endif
}