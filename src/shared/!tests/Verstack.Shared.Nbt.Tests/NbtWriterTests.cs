using Verstack.Shared.Nbt.Writer;

namespace Verstack.Shared.Nbt.Tests;

/// <summary>
/// Тесты <see cref="NbtWriter"/>: эталонные байты, свёренные вручную по wire-формату NBT
/// (minecraft.wiki/w/NBT_format). Покрывают networked/disk root, скаляры, вложенные compound,
/// list скаляров и list контейнеров (безымянные перегрузки).
///
/// Writer — ref struct, его нельзя передать через delegate/lambda (ref struct не escap'ит из области
/// объявления, а stackalloc-буферы — ref-scoped). Поэтому каждый тест конструирует writer инлайн:
/// 4 строки boilerplate — плата за GC-free на стеке. Это каноничный паттерн для тестирования ref struct.
/// </summary>
public class NbtWriterTests
{
    // ─────────────────────  Networked vs disk root  ─────────────────────

    /// <summary>
    /// Networked-root: только байт типа 0x0A (без имени) + TAG_End.
    /// </summary>
    [Fact]
    public void EmptyRoot_Networked_WritesTypeByteAndEnd()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames, networked: true);

        w.BeginRootCompound();
        w.EndCompound();

        Assert.Equal(ParseHex("0A 00"), w.Finish().ToArray());
    }

    /// <summary>
    /// Disk-root: байт типа 0x0A + Short=0 (пустое имя) + TAG_End.
    /// </summary>
    [Fact]
    public void EmptyRoot_Disk_WritesTypeByteEmptyNameAndEnd()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames, networked: false);

        w.BeginRootCompound();
        w.EndCompound();

        Assert.Equal(ParseHex("0A 00 00 00"), w.Finish().ToArray());
    }

    // ─────────────────────  Скаляры в Compound  ─────────────────────

    /// <summary>
    /// WriteInt в compound: [0x03][Short=5]["count"][42 BE]. Подробный разбор одного скаляра.
    /// </summary>
    [Fact]
    public void WriteInt_InCompound_WritesTypeNameAndValue()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .WriteInt("count"u8, 42)
        .EndCompound();

        // 0A                         root compound (networked)
        // 03                         TAG_Int
        // 00 05 63 6F 75 6E 74       Short=5 + "count"
        // 00 00 00 2A                42 BE
        // 00                         TAG_End
        Assert.Equal(ParseHex("0A 03 00 05 63 6F 75 6E 74 00 00 00 2A 00"), w.Finish().ToArray());
    }

    /// <summary>
    /// Все скалярные типы в одном compound. Имена однобуквенные — эталон компактнее.
    /// </summary>
    [Fact]
    public void AllScalarTypes_InCompound_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .WriteByte("b"u8, 1)          // TAG_Byte
            .WriteShort("s"u8, 2)         // TAG_Short
            .WriteInt("i"u8, 3)           // TAG_Int
            .WriteLong("l"u8, 4)          // TAG_Long
            .WriteFloat("f"u8, 1.0f)      // TAG_Float (1.0f = 0x3F800000)
            .WriteDouble("d"u8, 1.0)      // TAG_Double (1.0 = 0x3FF0000000000000)
            .WriteString("S"u8, "hi"u8)     // TAG_String
            .WriteBool("B"u8, true)       // TAG_Byte (bool → 0x01)
        .EndCompound();

        Assert.Equal(ParseHex(
            "0A"                                                    // root
            + " 01 00 01 62 01"                                     // Byte("b", 1)
            + " 02 00 01 73 00 02"                                  // Short("s", 2)
            + " 03 00 01 69 00 00 00 03"                            // Int("i", 3)
            + " 04 00 01 6C 00 00 00 00 00 00 00 04"                // Long("l", 4)
            + " 05 00 01 66 3F 80 00 00"                            // Float("f", 1.0f)
            + " 06 00 01 64 3F F0 00 00 00 00 00 00"                // Double("d", 1.0): 1.0 = 0x3FF0000000000000 (8 байт BE)
            + " 08 00 01 53 00 02 68 69"                            // String("S", "hi")
            + " 01 00 01 42 01"                                     // Bool("B", true)
            + " 00"),                                               // TAG_End
            w.Finish().ToArray());
    }

    // ─────────────────────  Вложенный Compound  ─────────────────────

    [Fact]
    public void NestedCompound_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .BeginCompound("nested"u8)
                .WriteBool("flag"u8, true)
            .EndCompound()
        .EndCompound();

        // 0A                              root (networked)
        // 0A 00 06 6E 65 73 74 65 64      nested compound: type + Short=6 + "nested"
        // 01 00 04 66 6C 61 67 01         Bool("flag", true): Byte + Short=4 + "flag" + 0x01
        // 00                              end nested
        // 00                              end root
        Assert.Equal(ParseHex("0A 0A 00 06 6E 65 73 74 65 64 01 00 04 66 6C 61 67 01 00 00"),
            w.Finish().ToArray());
    }

    // ─────────────────────  List скаляров  ─────────────────────

    /// <summary>
    /// List of String: заголовок [0x09][name][0x08][count=2] + элементы без имён и без type-байтов.
    /// </summary>
    [Fact]
    public void ListOfStrings_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .BeginList("items"u8, NbtTagType.String, 2)
                .WriteListItemString("a"u8)
                .WriteListItemString("b"u8)
            .EndList()
        .EndCompound();

        // 0A                              root
        // 09 00 05 69 74 65 6D 73         List: type + Short=5 + "items"
        // 08                              elementType = String
        // 00 00 00 02                     count = 2
        // 00 01 61                        "a": Short=1 + 'a' (без type-байта!)
        // 00 01 62                        "b"
        // 00                              end root (EndList ничего не пишет)
        Assert.Equal(ParseHex("0A 09 00 05 69 74 65 6D 73 08 00 00 00 02 00 01 61 00 01 62 00"),
            w.Finish().ToArray());
    }

    // ─────────────────────  List контейнеров (безымянные перегрузки)  ─────────────────────

    /// <summary>
    /// List of Compound: каждый элемент — безымянный compound БЕЗ ведущего type-байта
    /// (только children + TAG_End). Регресс-тест на баг с лишним type-байтом у list-элемента.
    /// </summary>
    [Fact]
    public void ListOfCompounds_WritesElementsWithoutTypeByte()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .BeginList("rows"u8, NbtTagType.Compound, 2)
                .BeginCompound()           // безымянный: только push frame, БЕЗ type-байта
                    .WriteInt("id"u8, 1)
                .EndCompound()
                .BeginCompound()
                    .WriteInt("id"u8, 2)
                .EndCompound()
            .EndList()
        .EndCompound();

        // 0A                              root
        // 09 00 04 72 6F 77 73 0A         List: type + Short=4 + "rows" + elementType=Compound
        // 00 00 00 02                     count = 2
        // 03 00 02 69 64 00 00 00 01      Int("id", 1) — БЕЗ ведущего 0A у compound-элемента
        // 00                              end compound 1
        // 03 00 02 69 64 00 00 00 02      Int("id", 2)
        // 00                              end compound 2
        // 00                              end root
        Assert.Equal(ParseHex(
            "0A"
            + " 09 00 04 72 6F 77 73 0A 00 00 00 02"
            + " 03 00 02 69 64 00 00 00 01 00"
            + " 03 00 02 69 64 00 00 00 02 00"
            + " 00"), w.Finish().ToArray());
    }

    // ─────────────────────  Полный пример из брифа  ─────────────────────

    /// <summary>
    /// Энд-ту-энд пример из брифа: скаляры + вложенный compound + list в одном root.
    /// Эталон свёлся вручную по wire-разбивке (65 байт).
    /// </summary>
    [Fact]
    public void FullExample_FromBrief_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var w = new NbtWriter(buffer, frames);

        w.BeginRootCompound()
            .WriteString("name"u8, "value"u8)
            .WriteInt("count"u8, 42)
            .BeginCompound("nested"u8)
                .WriteBool("flag"u8, true)
            .EndCompound()
            .BeginList("items"u8, NbtTagType.String, 2)
                .WriteListItemString("a"u8)
                .WriteListItemString("b"u8)
            .EndList()
        .EndCompound();

        Assert.Equal(ParseHex(
            "0A"                                                                    // root
            + " 08 00 04 6E 61 6D 65 00 05 76 61 6C 75 65"                         // String("name","value")
            + " 03 00 05 63 6F 75 6E 74 00 00 00 2A"                               // Int("count", 42)
            + " 0A 00 06 6E 65 73 74 65 64"                                        // Compound("nested")
            + " 01 00 04 66 6C 61 67 01"                                           // Bool("flag", true)
            + " 00"                                                                // end nested
            + " 09 00 05 69 74 65 6D 73 08 00 00 00 02"                           // List("items", String, 2)
            + " 00 01 61"                                                          // "a"
            + " 00 01 62"                                                          // "b"
            + " 00"),                                                              // end root
            w.Finish().ToArray());
    }

    // ─────────────────────  Хелпер  ─────────────────────

    private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
}