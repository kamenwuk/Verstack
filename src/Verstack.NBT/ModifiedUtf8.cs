using System.Runtime.CompilerServices;

namespace Verstack.Nbt;

/// <summary>
/// Кодировка Java modified UTF-8 — используется NBT для строк и имён тегов. НЕ обычный
/// <see cref="System.Text.Encoding.UTF8"/>: отличается двумя правилами.
///
/// Битовая разбивка (как в обычном UTF-8):
/// <code>
/// \u0001–\u007F     0xxxxxxx                          → 1 байт
/// \u0080–\u07FF     110xxxxx 10xxxxxx                 → 2 байта
/// \u0800–\uFFFF     1110xxxx 10xxxxxx 10xxxxxx        → 3 байта
/// </code>
/// Два отличия от обычного UTF-8:
/// 1. <c>\0</c> (U+0000) кодируется как <c>0xC0 0x80</c> (2 байта), а не одиночным <c>0x00</c> —
///    чтобы NUL-байт не встречался в payload (историческая совместимость с C-строками в Java).
/// 2. Символы вне BMP (&gt; U+FFFF) кодируются через UTF-16 суррогатную пару, и каждый суррогат
///    (0xD800–0xDFFF) записывается отдельным 3-байтным блоком — итого 6 байт на символ, а не 4.
///
/// Скалярная реализация: ASCII-символы (доминирующий случай для имён NBT-тегов) обрабатываются
/// первой быстрой веткой без накладных расходов. Векторизация (AVX2/SSE2 как в ObsidianMC) отложена.
/// </summary>
internal static class ModifiedUtf8
{
    /// <summary>
    /// Число байт в modified-UTF-8 представлении строки (не число символов).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(string value)
    {
        int count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\0')
                count += 2;
            else if (c <= 0x7F)
                count += 1;
            else if (c <= 0x7FF)
                count += 2;
            else
                // \u0800–\uFFFF, включая UTF-16 суррогаты: каждый кодируется 3 байтами.
                count += 3;
        }

        return count;
    }

    /// <summary>
    /// Записывает строку в <paramref name="destination"/> в modified-UTF-8. Caller обязан зарезервировать
    /// ровно <see cref="GetByteCount"/> байт.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(string value, Span<byte> destination)
    {
#if DEBUG
        // Двойной проход в DEBUG — плата за понятное сообщение при переполнении (caller ошибся с резервированием).
        // В Release Span сам бросит IndexOutOfRange, но сообщение будет менее информативным.
        int expected = GetByteCount(value);
        if (expected > destination.Length)
            throw new InvalidOperationException(
                $"[{nameof(ModifiedUtf8)}] Буфер слишком мал: нужно {expected} байт, доступно {destination.Length}.");
#endif

        int written = 0;
        for (var i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\0')
            {
                // \u0000 → 0xC0 0x80: ключевое отличие от обычного UTF-8.
                destination[written++] = 0xC0;
                destination[written++] = 0x80;
            }
            else if (c <= 0x7F)
            {
                destination[written++] = (byte)c;
            }
            else if (c <= 0x7FF)
            {
                destination[written++] = (byte)(0xC0 | (c >> 6));
                destination[written++] = (byte)(0x80 | (c & 0x3F));
            }
            else
            {
                // \u0800–\uFFFF. Суррогаты UTF-16 (0xD800–0xDFFF) попадают сюда и кодируются как
                // отдельные 3-байтные блоки — modified UTF-8 не комбинирует их в 4-байтные последовательности.
                destination[written++] = (byte)(0xE0 | (c >> 12));
                destination[written++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                destination[written++] = (byte)(0x80 | (c & 0x3F));
            }
        }
    }
}