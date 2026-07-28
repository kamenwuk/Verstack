namespace Verstack.Nbt.Tests;

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

    private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
}