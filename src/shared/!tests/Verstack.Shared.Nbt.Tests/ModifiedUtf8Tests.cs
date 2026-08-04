namespace Verstack.Shared.Nbt.Tests;

/// <summary>
/// Тесты <see cref="ModifiedUtf8"/>: тестовые векторы из спецификации NBT (minecraft.wiki/w/NBT_format)
/// плюс критические кейсы modified-UTF-8 — NUL-байт и суррогатные пары (отличия от обычного UTF-8).
/// </summary>
public class ModifiedUtf8Tests
{
    [Theory]
    [InlineData("a", 1)]           // ASCII: 1 байт
    [InlineData("é", 2)]           // U+00E9: 2 байта
    [InlineData("\0", 2)]          // U+0000 → C0 80: 2 байта (НЕ 1, как в обычном UTF-8!)
    [InlineData("€", 3)]           // U+20AC: 3 байта
    [InlineData("", 0)]            // пустая строка: 0 байт
    [InlineData("😀", 6)]          // U+1F600, суррогатная пара: 6 байт (НЕ 4, как в обычном UTF-8!)
    public void GetByteCount_ReturnsByteCount(string value, int expectedBytes)
    {
        Assert.Equal(expectedBytes, ModifiedUtf8.GetByteCount(value));
    }

    [Theory]
    [InlineData("a", "61")]
    [InlineData("é", "C3 A9")]
    [InlineData("\0", "C0 80")]
    [InlineData("€", "E2 82 AC")]
    [InlineData("", "")]
    public void Write_ProducesExpectedBytes(string value, string expectedHex)
    {
        byte[] expected = ParseHex(expectedHex);
        byte[] actual = new byte[ModifiedUtf8.GetByteCount(value)];

        ModifiedUtf8.Write(value, actual);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// U+1F600 (😀) в .NET-строке — два UTF-16 суррогата (D83D + DE00). Modified UTF-8 кодирует
    /// каждый суррогат отдельным 3-байтным блоком → 6 байт (а не 4, как в обычном UTF-8).
    /// Отдельный тест как защита регресса: это ключевое отличие двух кодировок.
    /// </summary>
    [Fact]
    public void Write_SurrogatePair_ProducesSixBytes()
    {
        string value = "😀";
        byte[] actual = new byte[ModifiedUtf8.GetByteCount(value)];

        ModifiedUtf8.Write(value, actual);

        Assert.Equal(ParseHex("ED A0 BD ED B8 80"), actual);
    }

    /// <summary>
    /// Смешанная строка проверяет, что ASCII/NUL/non-ASCII пишутся последовательно без смещения.
    /// </summary>
    [Fact]
    public void Write_MixedAsciiNulAndNonAscii_PreservesOrder()
    {
        // "a\0é" → 61 (a) + C0 80 (\0) + C3 A9 (é)
        string value = "a\0é";
        byte[] actual = new byte[ModifiedUtf8.GetByteCount(value)];

        ModifiedUtf8.Write(value, actual);

        Assert.Equal(ParseHex("61 C0 80 C3 A9"), actual);
    }
    
    /// <summary>
    /// Round-trip Write → Read(Span&lt;char&gt;) = исходная строка. Покрывает все ветви декодера:
    /// ASCII, 2-байт, NUL, 3-байт (вкл. суррогаты). Использует те же InlineData, что и
    /// Write_ProducesExpectedBytes — гарантирует, что любые байты, которые пишем, умеем читать обратно.
    /// </summary>
    [Theory]
    [InlineData("a")]                          // ASCII
    [InlineData("é")]                          // 2-байт
    [InlineData("\0")]                         // NUL: 0xC0 0x80 — ключевое отличие от UTF-8
    [InlineData("€")]                          // 3-байт
    [InlineData("")]                           // пустая
    [InlineData("a\0é")]                       // смешанная: порядок байт критичен
    [InlineData("😀")]                          // суррогатная пара: 6 байт → 2 char
    [InlineData("minecraft:chat_type")]        // реальный кейс NBT: identifier
    [InlineData("Имя-на-русском")]              // кириллица: сплошь 2-байт
    public void Read_IsInverseOfWrite(string value)
    {
        byte[] bytes = new byte[ModifiedUtf8.GetByteCount(value)];
        ModifiedUtf8.Write(value, bytes);

        // destination размера bytes.Length — гарантированный max (char-счёт ≤ byte-счёт).
        Span<char> destination = stackalloc char[bytes.Length == 0 ? 1 : bytes.Length];
        ModifiedUtf8.Read(bytes, destination, out int charsWritten);

        Assert.Equal(value, new string(destination[..charsWritten]));
    }

    /// <summary>
    /// Эталонные байты суррогатной пары (ED A0 BD ED B8 80) → символ 😀. Защита регресса на
    /// обратную сторону write-теста Write_SurrogatePair_ProducesSixBytes: тот же hex, читаем обратно.
    /// </summary>
    [Fact]
    public void Read_SurrogatePair_DecodesSixBytesToEmoji()
    {
        byte[] bytes = ParseHex("ED A0 BD ED B8 80");

        Span<char> destination = stackalloc char[6];
        ModifiedUtf8.Read(bytes, destination, out int charsWritten);

        Assert.Equal(2, charsWritten);                 // 2 UTF-16 char = суррогатная пара
        Assert.Equal("😀", new string(destination[..charsWritten]));
    }

    /// <summary>
    /// Буфер destination слишком мал → исключение (только в DEBUG). Защита от silent corruption:
    /// caller обязан резервировать ≥ source.Length.
    /// </summary>
    [Fact]
    public void Read_DestinationTooSmall_Throws()
    {
        byte[] bytes = ParseHex("C3 A9");   // "é", 2 байта
        Span<char> tooSmall = stackalloc char[1];   // нужно ≥ 2 (source.Length)

        bool threw = false;
        try { ModifiedUtf8.Read(bytes, tooSmall, out _); }
#if DEBUG
        catch (InvalidOperationException) { threw = true; }
#else
        // В Release проверки нет — Span сам бросит при переполнении.
        catch (Exception) { threw = true; }
#endif
        Assert.True(threw, "Ожидалось исключение при слишком малом destination.");
    }

    private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
}