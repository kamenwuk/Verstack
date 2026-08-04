using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>
/// Завершение (<see cref="Finish"/>), вход/выход контейнеров (Compound/List) и стек кадров. Скалярная запись —
/// в <c>NbtStreamWriterCompoundExtensions</c>/<c>NbtStreamWriterListExtensions</c>.
/// </summary>
public static class NbtWriterExtensions
{
    extension(ref NbtStreamWriter writer)
    {
        /// <summary>
        /// Завершает запись NBT и возвращает готовый буфер. Бросает при незакрытых Compound/List. Проверка
        /// всегда включена: отправка битого NBT крашнет клиент.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> Finish()
        {
            if (writer.Depth != 0)
            {
                throw new InvalidOperationException(
                    $"NBT не закрыт корректно! Осталось незакрытых контейнеров: {writer.Depth}. " +
                    "Убедитесь, что для каждого BeginCompound/BeginList вызван EndCompound/EndList.");
            }

            return writer.Buffer[..writer.Offset];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter BeginRootCompound()
        {
            writer.WriteTagType(NbtTagType.Compound);
            if (!writer.Networked)
                writer.WriteShortRaw(0);
            PushCompoundFrame(ref writer);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter BeginCompound(ReadOnlySpan<byte> nameUtf8)
        {
            writer.WriteNameAndType(NbtTagType.Compound, nameUtf8);
            PushCompoundFrame(ref writer);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter BeginCompound()
        {
            OnListItem(ref writer, NbtTagType.Compound);
            PushCompoundFrame(ref writer);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter EndCompound()
        {
            writer.WriteTagType(NbtTagType.End);
            PopFrame(ref writer);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter BeginList(ReadOnlySpan<byte> nameUtf8, NbtTagType elementType, int count)
        {
            writer.WriteNameAndType(NbtTagType.List, nameUtf8);
            writer.WriteListHeader(elementType, count);
            PushListFrame(ref writer, elementType, count);
            return ref writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter BeginList(NbtTagType elementType, int count)
        {
            OnListItem(ref writer, NbtTagType.List);
            writer.WriteListHeader(elementType, count);
            PushListFrame(ref writer, elementType, count);
            return ref writer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref NbtStreamWriter EndList()
        {
#if DEBUG
            if (writer.Depth == 0)
                throw new InvalidOperationException("EndList на пустом стеке.");
            ref NbtFrame frame = ref writer.Frames[writer.Depth - 1];
            if (frame.Container != NbtTagType.List)
                throw new InvalidOperationException($"EndList вызван вне List-контекста (текущий контейнер: {frame.Container}).");
            if (frame.ListRemaining != 0)
                throw new InvalidOperationException($"List закрыт с остатком: ожидалось ещё {frame.ListRemaining} элемент(ов).");
#endif
            PopFrame(ref writer);
            return ref writer;
        }

        /// <summary>Валидирует безымянный List-элемент и декрементирует остаток. <c>[Conditional]</c> снимает в Release.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        internal void OnListItem(NbtTagType type)
        {
            if (writer.Depth == 0)
                throw new InvalidOperationException("Скаляр без имени вызван вне List-контекста (стек пуст).");
            ref NbtFrame frame = ref writer.Frames[writer.Depth - 1];
            if (frame.Container != NbtTagType.List)
                throw new InvalidOperationException("Скаляр без имени вызван в Compound-контексте; используйте перегрузку с name.");
            if (frame.ListRemaining <= 0)
                throw new InvalidOperationException("List переполнен: заявлено элементов меньше, чем записано.");
            if (frame.ExpectedListItem != type)
                throw new InvalidOperationException($"Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
            frame.ListRemaining--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteNameAndType(NbtTagType type, ReadOnlySpan<byte> nameUtf8)
        {
            ValidateCompoundContext(ref writer, type);
            writer.WriteTagType(type);
            writer.WriteName(nameUtf8);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteStringPayload(ReadOnlySpan<byte> valueUtf8)
        {
#if DEBUG
            if (valueUtf8.Length > 32767)
                throw new InvalidOperationException($"TAG_String слишком длинная: {valueUtf8.Length} байт (max 32767).");
#endif
            writer.WriteShortRaw((short)valueUtf8.Length);
            writer.WriteSpan(valueUtf8);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteListHeader(NbtTagType elementType, int count)
        {
#if DEBUG
            if (count < 0)
                throw new InvalidOperationException($"Отрицательная длина List: {count}.");
#endif
            writer.WriteTagType(elementType);
            writer.WriteIntRaw(count);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteName(ReadOnlySpan<byte> nameUtf8)
        {
#if DEBUG
            if (nameUtf8.Length > 32767)
                throw new InvalidOperationException($"Имя тега слишком длинное: {nameUtf8.Length} байт (max 32767).");
#endif
            writer.WriteShortRaw((short)nameUtf8.Length);
            writer.WriteSpan(nameUtf8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushCompoundFrame(ref NbtStreamWriter writer) => PushFrame(ref writer, NbtTagType.Compound, NbtTagType.End, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushListFrame(ref NbtStreamWriter writer, NbtTagType elementType, int count) => PushFrame(ref writer, NbtTagType.List, elementType, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PushFrame(ref NbtStreamWriter writer, NbtTagType container, NbtTagType listItem, int remaining)
    {
#if DEBUG
        if (writer.Depth >= writer.Frames.Length)
            throw new InvalidOperationException($"Превышена глубина стека ({writer.Frames.Length}). Увеличьте frames в конструкторе.");
#endif
        writer.Frames[writer.Depth++] = new NbtFrame { Container = container, ExpectedListItem = listItem, ListRemaining = remaining };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PopFrame(ref NbtStreamWriter writer)
    {
#if DEBUG
        if (writer.Depth == 0)
            throw new InvalidOperationException("PopFrame на пустом стеке (лишний End* вызов).");
#endif
        writer.Depth--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateCompoundContext(ref NbtStreamWriter writer, NbtTagType type)
    {
        if (writer.Depth == 0)
            throw new InvalidOperationException($"Именованный тег {type} записан до BeginRootCompound/BeginCompound.");
        ref NbtFrame frame = ref writer.Frames[writer.Depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException($"Именованный тег {type} записан в List-контексте; используйте безымянную перегрузку.");
    }
}