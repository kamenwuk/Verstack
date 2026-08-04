using System.Runtime.CompilerServices;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>
/// GC-free writer NBT (Named Binary Tag) прямо в <c>Span&lt;byte&gt;</c>. Stateful <c>ref struct</c>:
/// помнит контекст вложенности (Compound/List) через стек <see cref="NbtFrame"/> и сам решает, писать ли
/// имя тегу и байт типа.
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

    /// <summary>
    /// Завершает запись NBT и возвращает готовый буфер.
    /// Бросает исключение, если есть незакрытые Compound/List.
    /// </summary>
    public ReadOnlySpan<byte> Finish()
    {
        // Оставляем проверку всегда включенной (даже в Release), 
        // потому что отправка битого NBT гарантированно крашнет клиент.
        // Это защитит продакшен от глупых ошибок.
        if (_depth != 0)
        {
            throw new InvalidOperationException(
                $"NBT не закрыт корректно! Осталось незакрытых контейнеров: {_depth}. " +
                $"Убедитесь, что для каждого BeginCompound/BeginList вызван EndCompound/EndList.");
        }

        return _buffer[.._offset];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void BeginRootCompoundInternal()
    {
        WriteTagType(NbtTagType.Compound);
        if (!_networked)
            WriteShortRaw(0);
        PushCompoundFrame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EndListInternal()
    {
#if DEBUG
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException(
                $"EndList вызван вне List-контекста (текущий контейнер: {frame.Container}).");
        if (frame.ListRemaining != 0)
            throw new InvalidOperationException(
                $"List закрыт с остатком: ожидалось ещё {frame.ListRemaining} элемент(ов).");
#endif
        PopFrame();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Conditional("DEBUG")]
    internal void OnListItemInternal(NbtTagType type)
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException($"Скаляр без имени вызван вне List-контекста (стек пуст).");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.List)
            throw new InvalidOperationException($"Скаляр без имени вызван в Compound-контексте; используйте перегрузку с name.");
        if (frame.ListRemaining <= 0)
            throw new InvalidOperationException($"List переполнен: заявлено элементов меньше, чем записано.");
        if (frame.ExpectedListItem != type)
            throw new InvalidOperationException($"Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
        frame.ListRemaining--;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteNameAndType(NbtTagType type, ReadOnlySpan<byte> nameUtf8)
    {
#if DEBUG
        ValidateCompoundContext(type);
#endif
        WriteTagType(type);
        WriteName(nameUtf8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteListHeader(NbtTagType elementType, int count)
    {
#if DEBUG
        if (count < 0)
            throw new InvalidOperationException($"Отрицательная длина List: {count}.");
#endif
        WriteTagType(elementType);
        WriteIntRaw(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteName(ReadOnlySpan<byte> nameUtf8)
    {
#if DEBUG
        if (nameUtf8.Length > MAX_STRING_LENGTH)
            throw new InvalidOperationException($"Имя тега слишком длинное: {nameUtf8.Length} байт (max {MAX_STRING_LENGTH}).");
#endif
        WriteShortRaw((short)nameUtf8.Length);
        WriteSpan(nameUtf8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteStringPayload(ReadOnlySpan<byte> valueUtf8)
    {
#if DEBUG
        if (valueUtf8.Length > MAX_STRING_LENGTH)
            throw new InvalidOperationException($"TAG_String слишком длинная: {valueUtf8.Length} байт (max {MAX_STRING_LENGTH}).");
#endif
        WriteShortRaw((short)valueUtf8.Length);
        WriteSpan(valueUtf8);
    }

    // ─────────────────────  Raw-запись BE-скаляров  ─────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteTagType(NbtTagType type) => WriteByteRaw((byte)type);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteByteRaw(byte value) { _buffer[_offset] = value; _offset += 1; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteShortRaw(short value) { BinaryPrimitives.WriteInt16BigEndian(_buffer[_offset..], value); _offset += 2; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteIntRaw(int value) { BinaryPrimitives.WriteInt32BigEndian(_buffer[_offset..], value); _offset += 4; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteLongRaw(long value) { BinaryPrimitives.WriteInt64BigEndian(_buffer[_offset..], value); _offset += 8; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteSpan(ReadOnlySpan<byte> value) { value.CopyTo(_buffer[_offset..]); _offset += value.Length; }

    // ─────────────────────────  Управление стеком  ─────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PushCompoundFrame() => PushFrame(NbtTagType.Compound, NbtTagType.End, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PushListFrame(NbtTagType elementType, int count) => PushFrame(NbtTagType.List, elementType, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushFrame(NbtTagType container, NbtTagType listItem, int remaining)
    {
#if DEBUG
        if (_depth >= _frames.Length)
            throw new InvalidOperationException($"Превышена глубина стека ({_frames.Length}). Увеличьте frames в конструкторе.");
#endif
        _frames[_depth++] = new NbtFrame { Container = container, ExpectedListItem = listItem, ListRemaining = remaining };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PopFrame()
    {
#if DEBUG
        if (_depth == 0)
            throw new InvalidOperationException($"PopFrame на пустом стеке (лишний End* вызов).");
#endif
        _depth--;
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCompoundContext(NbtTagType type)
    {
        if (_depth == 0)
            throw new InvalidOperationException($"Именованный тег {type} записан до BeginRootCompound/BeginCompound.");
        ref NbtFrame frame = ref _frames[_depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException($"Именованный тег {type} записан в List-контексте; используйте безымянную перегрузку.");
    }
#endif
}