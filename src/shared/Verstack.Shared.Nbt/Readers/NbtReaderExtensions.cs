using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// Вход/выход контейнеров (Compound/List), peek имени тега (<see cref="ReadTagName"/>) и пропуск тегов.
/// Безымянные List-скаляры и payload после peek — в <c>NbtReaderListScalarExtensions</c>/<c>NbtReaderPayloadExtensions</c>.
/// </summary>
public static class NbtReaderExtensions
{
    extension(ref NbtStreamReader reader)
    {
        // ─────────────────────────  Compound  ─────────────────────────

        /// <summary>Входит в корневой compound: type-байт 0x0A (+ Short=0 для disk-формата).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterRootCompound()
        {
            NbtTagType type = reader.ReadTagType();
#if DEBUG
            if (type != NbtTagType.Compound)
                throw new InvalidOperationException($"Корневой тег не Compound: {type}.");
#endif
            if (!reader.Networked)
            {
                short nameLen = reader.ReadShortRaw();
#if DEBUG
                if (nameLen != 0)
                    throw new InvalidOperationException($"Disk-root compound ожидает пустое имя (Short=0), получено {nameLen}.");
#endif
            }
            PushCompoundFrame(ref reader);
        }

        /// <summary>
        /// Входит в compound: push frame без чтения. В Compound — после <see cref="ReadTagName"/> (type+name уже
        /// прочитаны), в List — как элемент List.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterCompound()
        {
            OnEnterContainer(ref reader, NbtTagType.Compound);
            PushCompoundFrame(ref reader);
        }

        /// <summary>Входит в compound после peek: push frame (type+name уже прочитаны в <see cref="ReadTagName"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterCompound(out string name)
        {
            name = string.Empty;
            PushCompoundFrame(ref reader);
        }

        /// <summary>Закрывает compound: читает и валидирует TAG_End (0x00), затем pop frame.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitCompound()
        {
#if DEBUG
            if (reader.Depth == 0)
                throw new InvalidOperationException("ExitCompound на пустом стеке (лишний Exit).");
            ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
            if (frame.Container != NbtTagType.Compound)
                throw new InvalidOperationException("ExitCompound в List-контексте (используйте ExitList).");
#endif
            NbtTagType end = reader.ReadTagType();
#if DEBUG
            if (end != NbtTagType.End)
                throw new InvalidOperationException(
                    $"Ожидался TAG_End (0x00) в конце compound, получен {end}. " +
                    "Возможно, caller не прочитал все теги compound (остались непрочитанные теги).");
#endif
            PopFrame(ref reader);
        }

        // ─────────────────────────  List  ─────────────────────────

        /// <summary>Входит в List: [elementType+count] (без type/name) + push frame.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterList(out NbtTagType elementType, out int count)
        {
            OnEnterContainer(ref reader, NbtTagType.List);
            elementType = reader.ReadTagType();
            count = reader.ReadIntRaw();
            PushListFrame(ref reader, elementType, count);
        }

        /// <summary>Закрывает List: длина уже в заголовке, в DEBUG проверяет остаток 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitList()
        {
#if DEBUG
            if (reader.Depth == 0)
                throw new InvalidOperationException("ExitList на пустом стеке (лишний Exit).");
            ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
            if (frame.Container != NbtTagType.List)
                throw new InvalidOperationException("ExitList в Compound-контексте (используйте ExitCompound).");
            if (frame.ListRemaining != 0)
                throw new InvalidOperationException($"List закрыт с остатком: ожидалось ещё {frame.ListRemaining} элемент(ов).");
#endif
            PopFrame(ref reader);
        }

        /// <summary>Осталось прочитать элементов в текущем List.</summary>
        public int ListRemaining
        {
            get
            {
#if DEBUG
                if (reader.Depth == 0 || reader.Frames[reader.Depth - 1].Container != NbtTagType.List)
                    throw new InvalidOperationException("ListRemaining вызван вне List-контекста.");
#endif
                return reader.Frames[reader.Depth - 1].ListRemaining;
            }
        }

        // ───────────────  Sequental: peek тега в Compound  ───────────────

        /// <summary>
        /// В Compound-контексте: читает [type-байт + имя], НЕ потребляя payload. Возвращает <see cref="NbtTagType.End"/>
        /// при достижении конца compound. При End делает rollback — TAG_End <b>не потребляется</b>, его прочитает
        /// <see cref="ExitCompound"/>. Имя — zero-copy срез modified-UTF-8; сравнение через SequenceEqual.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> utf8Name)
        {
#if DEBUG
            ValidateCompoundContext(ref reader);
#endif
            type = reader.ReadTagType();
            if (type == NbtTagType.End)
            {
                reader.Rollback(1);
                utf8Name = default;
                return;
            }
            utf8Name = reader.ReadNameBytes();
        }

        // ─────────────────────  Пропуск payload  ─────────────────────

        /// <summary>
        /// Пропускает payload тега <paramref name="type"/> (для контейнеров — рекурсивно). type-байт и имя
        /// должны быть уже потреблены через <see cref="ReadTagName"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SkipPayload(NbtTagType type)
        {
            switch (type)
            {
                case NbtTagType.Byte: reader.Advance(1); break;
                case NbtTagType.Short: reader.Advance(2); break;
                case NbtTagType.Int:
                case NbtTagType.Float: reader.Advance(4); break;
                case NbtTagType.Long:
                case NbtTagType.Double: reader.Advance(8); break;
                case NbtTagType.String:
                    reader.Advance(reader.ReadShortRaw());
                    break;
                case NbtTagType.ByteArray: reader.Advance(reader.ReadIntRaw() * 1); break;
                case NbtTagType.IntArray: reader.Advance(reader.ReadIntRaw() * 4); break;
                case NbtTagType.LongArray: reader.Advance(reader.ReadIntRaw() * 8); break;
                case NbtTagType.List: SkipList(ref reader); break;
                case NbtTagType.Compound: SkipCompound(ref reader); break;
            }
        }

        /// <summary>Пропускает оставшиеся теги текущего Compound до TAG_End (End не потребляется — закрывайте через <see cref="ExitCompound"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SkipRemaining()
        {
#if DEBUG
            ValidateCompoundContext(ref reader);
#endif
            while (true)
            {
                reader.ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> _);
                if (type == NbtTagType.End) return;
                reader.SkipPayload(type);
            }
        }

        // ─────────────────────  Гварды контекста (internal — для ListScalar/Array extensions)  ─────────────────────

        /// <summary>Валидирует безымянный List-скаляр и декрементирует остаток. <c>[Conditional]</c> снимает в Release.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        internal void OnListScalar(NbtTagType type)
        {
            if (reader.Depth == 0)
                throw new InvalidOperationException("Скаляр без имени вызван вне List-контекста.");
            ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
            if (frame.Container != NbtTagType.List)
                throw new InvalidOperationException("Скаляр без имени вызван в Compound-контексте; используйте ReadTagName + ReadXxxPayload.");
            if (frame.ListRemaining <= 0)
                throw new InvalidOperationException("List переполнен: записано больше заявленного.");
            if (frame.ExpectedListItem != type)
                throw new InvalidOperationException($"Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
            frame.ListRemaining--;
        }

        /// <summary>Валидирует вход в контейнер. В Compound-after-peek — no-op, в List — декремент остатка.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        internal void OnEnterContainer(NbtTagType type)
        {
            if (reader.Depth == 0) return;
            ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
            if (frame.Container != NbtTagType.List) return;
            if (frame.ListRemaining <= 0)
                throw new InvalidOperationException("List переполнен: записано больше заявленного.");
            if (frame.ExpectedListItem != type)
                throw new InvalidOperationException($"Несовпадение типа List-элемента: ожидался {frame.ExpectedListItem}, получен {type}.");
            frame.ListRemaining--;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushCompoundFrame() => reader.PushFrame(NbtTagType.Compound, NbtTagType.End, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PushListFrame(NbtTagType elementType, int count) => reader.PushFrame(NbtTagType.List, elementType, count);
    }

    extension(ref NbtStreamReader reader)
    {
        private void PushFrame(NbtTagType container, NbtTagType listItem, int remaining)
        {
#if DEBUG
            if (reader.Depth >= reader.Frames.Length)
                throw new InvalidOperationException($"Превышена глубина стека ({reader.Frames.Length}). Увеличьте frames в конструкторе.");
#endif
            reader.Frames[reader.Depth++] = new NbtFrame { Container = container, ExpectedListItem = listItem, ListRemaining = remaining };
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipList()
        {
            NbtTagType elementType = reader.ReadTagType();
            int count = reader.ReadIntRaw();
            for (int i = 0; i < count; i++)
                reader.SkipPayload(elementType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipCompound()
        {
            while (true)
            {
                NbtTagType t = reader.ReadTagType();
                if (t == NbtTagType.End) return;
                reader.ReadNameBytes();
                reader.SkipPayload(t);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PopFrame(this ref NbtStreamReader reader)
    {
#if DEBUG
        if (reader.Depth == 0)
            throw new InvalidOperationException("PopFrame на пустом стеке (лишний End* вызов).");
#endif
        reader.Depth--;
    }

#if DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateCompoundContext(ref NbtStreamReader reader)
    {
        if (reader.Depth == 0)
            throw new InvalidOperationException("ReadTagName вызван до EnterRootCompound (стек пуст).");
        ref NbtFrame frame = ref reader.Frames[reader.Depth - 1];
        if (frame.Container != NbtTagType.Compound)
            throw new InvalidOperationException("ReadTagName в List-контексте; используйте безымянные перегрузки.");
    }
#endif
}