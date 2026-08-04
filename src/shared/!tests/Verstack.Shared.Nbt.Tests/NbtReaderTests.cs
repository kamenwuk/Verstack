using Verstack.Shared.Nbt.Writer;

namespace Verstack.Shared.Nbt.Tests;

/// <summary>
/// Тесты <see cref="NbtReader"/>: кормим эталонными байтами из <see cref="NbtWriterTests"/> (те же hex)
/// и проверяем прочитанные значения. Дополнительно — round-trip (writer пишет → reader читает) и
/// sequental-обход с peek+payload.
///
/// API reader'а — zero-alloc: имена тегов возвращаются как <c>ReadOnlySpan&lt;byte&gt;</c> (zero-copy
/// срез из буфера), сравниваются с литералами через <c>SequenceEqual("name"u8)</c>. Строковые значения
/// читаются в caller'ов <c>Span&lt;char&gt;</c> через декодер. Для ассертов удобно собирать строку через
/// <c>new string(span[..n])</c> — в тестах аллокация допустима.
///
/// Boilerplate 4 строки на тест — плата за GC-free ref struct (см. <see cref="NbtWriterTests"/>).
/// </summary>
public class NbtReaderTests
{
    // ─────────────────────  Networked vs disk root  ─────────────────────

    /// <summary>
    /// Networked-root: 0A 00. Reader читает type 0x0A (без имени), входит в пустой compound,
    /// сразу видит TAG_End и выходит.
    /// </summary>
    [Fact]
    public void EmptyRoot_Networked_EntersAndExits()
    {
        ReadOnlySpan<byte> bytes = ParseHex("0A 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames, networked: true);

        r.EnterRootCompound();
        r.ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> name);
        Assert.Equal(NbtTagType.End, type);
        Assert.True(name.IsEmpty);
        r.ExitCompound();

        Assert.Equal(bytes.Length, r.Read);   // весь буфер прочитан
    }

    /// <summary>
    /// Disk-root: 0A 00 00 00. Reader читает 0x0A + Short=0 (пустое имя), дальше как networked.
    /// </summary>
    [Fact]
    public void EmptyRoot_Disk_ReadsShortZeroName()
    {
        ReadOnlySpan<byte> bytes = ParseHex("0A 00 00 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames, networked: false);

        r.EnterRootCompound();
        r.ReadTagName(out NbtTagType type, out _);
        Assert.Equal(NbtTagType.End, type);
        r.ExitCompound();

        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  Скаляры в Compound (sequental: peek + payload)  ─────────────────────

    /// <summary>
    /// WriteInt в compound из writer-теста: 0A 03 00 05 636F756E74 0000002A 00.
    /// Reader: enter root → peek тег (Int, имя "count") → read payload (42) → peek End → exit.
    /// Имя — zero-copy byte-slice, сравнение через SequenceEqual("count"u8).
    /// </summary>
    [Fact]
    public void ReadInt_InCompound_PeekAndPayload()
    {
        ReadOnlySpan<byte> bytes = ParseHex("0A 03 00 05 63 6F 75 6E 74 00 00 00 2A 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        r.ReadTagName(out NbtTagType type, out ReadOnlySpan<byte> name);
        Assert.Equal(NbtTagType.Int, type);
        Assert.True(name.SequenceEqual("count"u8));

        int value = r.ReadIntPayload();
        Assert.Equal(42, value);

        r.ReadTagName(out type, out _);
        Assert.Equal(NbtTagType.End, type);
        r.ExitCompound();
    }

    /// <summary>
    /// Все скалярные типы: тот же hex, что и в AllScalarTypes_InCompound_WritesExpectedBytes.
    /// Проверяем sequental-обход всех 8 типов и корректность декодирования каждого. Имена однобуквенные.
    /// </summary>
    [Fact]
    public void AllScalarTypes_InCompound_ReadsAllValues()
    {
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 01 00 01 62 01"                                     // Byte("b", 1)
            + " 02 00 01 73 00 02"                                  // Short("s", 2)
            + " 03 00 01 69 00 00 00 03"                            // Int("i", 3)
            + " 04 00 01 6C 00 00 00 00 00 00 00 04"                // Long("l", 4)
            + " 05 00 01 66 3F 80 00 00"                            // Float("f", 1.0f)
            + " 06 00 01 64 3F F0 00 00 00 00 00 00"                // Double("d", 1.0)
            + " 08 00 01 53 00 02 68 69"                            // String("S", "hi")
            + " 01 00 01 42 01"                                     // Bool("B", true)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();

        r.ReadTagName(out var t, out var n); Assert.Equal(NbtTagType.Byte, t);  Assert.True(n.SequenceEqual("b"u8)); Assert.Equal((sbyte)1, r.ReadBytePayload());
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Short, t); Assert.True(n.SequenceEqual("s"u8)); Assert.Equal((short)2, r.ReadShortPayload());
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Int, t);   Assert.True(n.SequenceEqual("i"u8)); Assert.Equal(3, r.ReadIntPayload());
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Long, t);  Assert.True(n.SequenceEqual("l"u8)); Assert.Equal(4L, r.ReadLongPayload());
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Float, t); Assert.True(n.SequenceEqual("f"u8)); Assert.Equal(1.0f, r.ReadFloatPayload());
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Double, t);Assert.True(n.SequenceEqual("d"u8)); Assert.Equal(1.0, r.ReadDoublePayload());

        // String value — Span<char> через ReadStringPayload (Compound after peek).
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.String, t);Assert.True(n.SequenceEqual("S"u8));
        Span<char> strBuf = stackalloc char[16];
        r.ReadStringPayload(strBuf, out int charsWritten);
        Assert.Equal("hi", new string(strBuf[..charsWritten]));

        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Byte, t);  Assert.True(n.SequenceEqual("B"u8)); Assert.True(r.ReadBoolPayload());

        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  Вложенный Compound  ─────────────────────

    [Fact]
    public void NestedCompound_EntersAndReadsNested()
    {
        ReadOnlySpan<byte> bytes = ParseHex("0A 0A 00 06 6E 65 73 74 65 64 01 00 04 66 6C 61 67 01 00 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        r.ReadTagName(out var t, out var n);
        Assert.Equal(NbtTagType.Compound, t);
        Assert.True(n.SequenceEqual("nested"u8));

        r.EnterCompound();              // peek уже сделан — просто push frame
        r.ReadTagName(out t, out n);
        Assert.Equal(NbtTagType.Byte, t);
        Assert.True(n.SequenceEqual("flag"u8));
        Assert.True(r.ReadBoolPayload());

        r.ReadTagName(out t, out _);
        Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();               // закрываем nested

        r.ReadTagName(out t, out _);
        Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();               // закрываем root
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  List скаляров  ─────────────────────

    /// <summary>
    /// List of String: peek List → EnterList → читаем 2 String-элемента (безымянные перегрузки)
    /// → ExitList. Симметрия с ListOfStrings_WritesExpectedBytes writer-теста.
    /// </summary>
    [Fact]
    public void ListOfStrings_ReadsElementsWithoutTypeByte()
    {
        ReadOnlySpan<byte> bytes = ParseHex("0A 09 00 05 69 74 65 6D 73 08 00 00 00 02 00 01 61 00 01 62 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        r.ReadTagName(out var t, out var n);
        Assert.Equal(NbtTagType.List, t);
        Assert.True(n.SequenceEqual("items"u8));

        r.EnterList(out NbtTagType elemType, out int count);
        Assert.Equal(NbtTagType.String, elemType);
        Assert.Equal(2, count);

        Span<char> sb = stackalloc char[8];
        r.ReadString(sb, out int cw1); Assert.Equal("a", new string(sb[..cw1]));
        r.ReadString(sb, out int cw2); Assert.Equal("b", new string(sb[..cw2]));

        r.ExitList();
        r.ReadTagName(out t, out _);
        Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  List контейнеров  ─────────────────────

    /// <summary>
    /// List of Compound: каждый элемент — безымянный compound без type-байта.
    /// Симметрия с ListOfCompounds_WritesElementsWithoutTypeByte.
    /// </summary>
    [Fact]
    public void ListOfCompounds_ReadsElementsWithoutTypeByte()
    {
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 09 00 04 72 6F 77 73 0A 00 00 00 02"
            + " 03 00 02 69 64 00 00 00 01 00"     // Int("id",1) + End
            + " 03 00 02 69 64 00 00 00 02 00"     // Int("id",2) + End
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        r.ReadTagName(out var t, out _);
        Assert.Equal(NbtTagType.List, t);

        r.EnterList(out var elemType, out int count);
        Assert.Equal(NbtTagType.Compound, elemType);
        Assert.Equal(2, count);

        // Элемент 1
        r.EnterCompound();   // безымянный: OnEnterContainer + push frame, ничего не читает
        r.ReadTagName(out t, out var n); Assert.Equal(NbtTagType.Int, t); Assert.True(n.SequenceEqual("id"u8));
        Assert.Equal(1, r.ReadIntPayload());
        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();

        // Элемент 2
        r.EnterCompound();
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Int, t); Assert.True(n.SequenceEqual("id"u8));
        Assert.Equal(2, r.ReadIntPayload());
        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();

        r.ExitList();
        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  SkipPayload (база для lookup)  ─────────────────────

    /// <summary>
    /// В compound с 3 тегами {Byte skip, Int keep, String skip} — пропускаем первый, читаем второй,
    /// пропускаем третий. Защита регресса на SkipPayload для скаляров.
    /// </summary>
    [Fact]
    public void SkipPayload_SkipsScalarAndAdvancesCursor()
    {
        // root { Byte("x", 1), Int("y", 42), String("z","hi") }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 01 00 01 78 01"                  // Byte("x", 1)
            + " 03 00 01 79 00 00 00 2A"         // Int("y", 42)
            + " 08 00 01 7A 00 02 68 69"         // String("z","hi")
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();

        // Тег 1: peek, не наш — skip
        r.ReadTagName(out var t, out var n);
        Assert.True(n.SequenceEqual("x"u8));
        r.SkipPayload(t);

        // Тег 2: peek, наш — читаем
        r.ReadTagName(out t, out n);
        Assert.True(n.SequenceEqual("y"u8));
        Assert.Equal(42, r.ReadIntPayload());

        // Тег 3: peek, не наш — skip
        r.ReadTagName(out t, out n);
        Assert.True(n.SequenceEqual("z"u8));
        r.SkipPayload(t);

        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// SkipPayload для вложенного Compound: пропускаем compound-тег целиком (рекурсивно).
    /// </summary>
    [Fact]
    public void SkipPayload_SkipsNestedCompound()
    {
        // root { Compound("skip", {Int("a",1)}), Int("keep", 7) }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 0A 00 04 73 6B 69 70"            // Compound("skip")
            + " 03 00 01 61 00 00 00 01"         //   Int("a", 1)
            + " 00"                              //   End skip
            + " 03 00 04 6B 65 65 70 00 00 00 07"// Int("keep", 7)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();

        r.ReadTagName(out var t, out var n);
        Assert.True(n.SequenceEqual("skip"u8));
        Assert.Equal(NbtTagType.Compound, t);
        r.SkipPayload(t);   // пропускаем compound целиком (рекурсивно)

        r.ReadTagName(out t, out n);
        Assert.True(n.SequenceEqual("keep"u8));
        Assert.Equal(7, r.ReadIntPayload());

        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  Полный пример из брифа (round-trip)  ─────────────────────

    /// <summary>
    /// Round-trip полного примера из брифа: скаляры + вложенный compound + list в одном root.
    /// Эталон — тот же hex, что и в FullExample_FromBrief_WritesExpectedBytes writer-теста.
    /// </summary>
    [Fact]
    public void FullExample_ReadsBackWhatWriterWrote()
    {
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 08 00 04 6E 61 6D 65 00 05 76 61 6C 75 65"                         // String("name","value")
            + " 03 00 05 63 6F 75 6E 74 00 00 00 2A"                               // Int("count", 42)
            + " 0A 00 06 6E 65 73 74 65 64"                                        // Compound("nested")
            + " 01 00 04 66 6C 61 67 01"                                           //   Bool("flag", true)
            + " 00"                                                                //   end nested
            + " 09 00 05 69 74 65 6D 73 08 00 00 00 02"                           // List("items", String, 2)
            + " 00 01 61"                                                          //   "a"
            + " 00 01 62"                                                          //   "b"
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();

        // name
        r.ReadTagName(out var t, out var n); Assert.Equal(NbtTagType.String, t); Assert.True(n.SequenceEqual("name"u8));
        Span<char> nameBuf = stackalloc char[16];
        r.ReadStringPayload(nameBuf, out int nameLen);
        Assert.Equal("value", new string(nameBuf[..nameLen]));

        // count
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Int, t); Assert.True(n.SequenceEqual("count"u8));
        Assert.Equal(42, r.ReadIntPayload());

        // nested
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Compound, t); Assert.True(n.SequenceEqual("nested"u8));
        r.EnterCompound();
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.Byte, t); Assert.True(n.SequenceEqual("flag"u8));
        Assert.True(r.ReadBoolPayload());
        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();

        // items
        r.ReadTagName(out t, out n); Assert.Equal(NbtTagType.List, t); Assert.True(n.SequenceEqual("items"u8));
        r.EnterList(out var elemType, out int count);
        Assert.Equal(NbtTagType.String, elemType); Assert.Equal(2, count);
        Span<char> itemBuf = stackalloc char[8];
        r.ReadString(itemBuf, out int cw1); Assert.Equal("a", new string(itemBuf[..cw1]));
        r.ReadString(itemBuf, out int cw2); Assert.Equal("b", new string(itemBuf[..cw2]));
        r.ExitList();

        r.ReadTagName(out t, out _); Assert.Equal(NbtTagType.End, t);
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  Lookup по имени (byte-compare)  ─────────────────────

    /// <summary>
    /// Базовый lookup: compound {Byte("x",1), Int("y",42), String("z","hi")} → TryReadInt("y") = 42.
    /// SkipPayload пропускает x, находит y, читает. Имя — byte-pattern "y"u8.
    /// </summary>
    [Fact]
    public void TryReadInt_Found_ReturnsValueAndAdvances()
    {
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 01 00 01 78 01"                  // Byte("x", 1)
            + " 03 00 01 79 00 00 00 2A"         // Int("y", 42)
            + " 08 00 01 7A 00 02 68 69"         // String("z","hi")
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        Assert.True(r.TryReadInt("y"u8, out int value));
        Assert.Equal(42, value);

        r.SkipRemaining();      // пропустить "z" (не интересует)
        r.ExitCompound();       // читает End, pop
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// Lookup отсутствующего имени → false, но End НЕ потреблён (rollback), можно продолжать.
    /// Это ключевое свойство robust lookup: один промах не закрывает compound.
    /// </summary>
    [Fact]
    public void TryReadInt_NotFound_DoesNotConsumeEnd()
    {
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 01 00 01 78 01"                  // Byte("x", 1)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        Assert.False(r.TryReadInt("missing"u8, out int value));
        Assert.Equal(0, value);   // default

        // End НЕ потреблён — можно делать ещё lookup (тоже промахнётся, но без исключения)
        Assert.False(r.TryReadInt("also_missing"u8, out _));

        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// Множественные lookup в одном compound — основная сценарий Verstack.Vanilla. Эмулирует
    /// чтение полей реестра: name, count, type — каждое по имени, порядок независим от потока.
    /// </summary>
    [Fact]
    public void MultipleLookups_ReadAllFieldsByName()
    {
        // { String("name","chat"), Int("count",5), Bool("enabled", true) }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 08 00 04 6E 61 6D 65 00 04 63 68 61 74"          // String("name","chat")
            + " 03 00 05 63 6F 75 6E 74 00 00 00 05"             // Int("count",5)
            + " 01 00 07 65 6E 61 62 6C 65 64 01"                // Bool("enabled", true)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        Span<char> nameBuf = stackalloc char[32];
        Assert.True(r.TryReadString("name"u8, nameBuf, out int nameLen)); Assert.Equal("chat", new string(nameBuf[..nameLen]));
        Assert.True(r.TryReadInt("count"u8, out int count));          Assert.Equal(5, count);
        Assert.True(r.TryReadBool("enabled"u8, out bool enabled));    Assert.True(enabled);

        // Четвёртый lookup — отсутствующее поле. Не должно ломать:
        Assert.False(r.TryReadLong("timestamp"u8, out _));

        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// Lookup с неверным типом → InvalidOperationException (не молчаливое false). Caller обязан
    /// знать схему: если просит Int под именем "y", а там String — это баг в caller'е.
    /// </summary>
    [Fact]
    public void TryReadInt_TypeMismatch_Throws()
    {
        // { String("y","hi") } — caller ждёт Int, а там String.
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 08 00 01 79 00 02 68 69"          // String("y","hi")
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();

        bool threw = false;
        try { r.TryReadInt("y"u8, out _); }
        catch (InvalidOperationException) { threw = true; }
        Assert.True(threw, "Ожидалось InvalidOperationException при несовпадении типа тега.");
    }

    /// <summary>
    /// TryEnterCompound: находит вложенный compound по имени, входит в него. После чтения
    /// внутреннего поля — ExitCompound внутреннего, затем продолжаем lookup во внешнем.
    /// </summary>
    [Fact]
    public void TryEnterCompound_FindsAndEntersNested()
    {
        // { Int("outer",7), Compound("inner", {Bool("flag",true)}) }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 03 00 05 6F 75 74 65 72 00 00 00 07"             // Int("outer",7)
            + " 0A 00 05 69 6E 6E 65 72"                          // Compound("inner")
            + " 01 00 04 66 6C 61 67 01"                          //   Bool("flag",true)
            + " 00"                                               //   end inner
            + " 00");                                             //   end root
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        Assert.True(r.TryReadInt("outer"u8, out int outer));        Assert.Equal(7, outer);
        Assert.True(r.TryEnterCompound("inner"u8));
        {
            Assert.True(r.TryReadBool("flag"u8, out bool flag));    Assert.True(flag);
            r.ExitCompound();   // закрываем inner
        }
        r.ExitCompound();       // закрываем root
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// TryEnterList: находит List по имени, входит, читает элементы, выходит.
    /// </summary>
    [Fact]
    public void TryEnterList_FindsAndEntersList()
    {
        // { List("tags", String, 2, ["a","b"]) }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 09 00 04 74 61 67 73 08 00 00 00 02"     // List("tags",String,2)
            + " 00 01 61"                                 //   "a"
            + " 00 01 62"                                 //   "b"
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        Assert.True(r.TryEnterList("tags"u8, out var elemType, out int count));
        Assert.Equal(NbtTagType.String, elemType);
        Assert.Equal(2, count);
        Span<char> sb = stackalloc char[8];
        r.ReadString(sb, out int cw1); Assert.Equal("a", new string(sb[..cw1]));
        r.ReadString(sb, out int cw2); Assert.Equal("b", new string(sb[..cw2]));
        r.ExitList();
        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    /// <summary>
    /// Lookup в List-контексте → исключение (там нет имён, lookup бессмысленен). Защита API-misuse.
    /// </summary>
    [Fact]
    public void TryReadInt_InListContext_Throws()
    {
        // { List("items", Byte, 1, [1]) }
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 09 00 05 69 74 65 6D 73 01 00 00 00 01"   // List("items",Byte,1)
            + " 01"                                         //   Byte(1)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        r.TryEnterList("items"u8, out _, out _);

        bool threw = false;
        try { r.TryReadByte("x"u8, out _); }
        catch (InvalidOperationException) { threw = true; }
        Assert.True(threw, "Ожидалось InvalidOperationException при lookup в List-контексте.");

        r.ReadByte();    // потребить элемент (List ожидает Byte, не String!)
        r.ExitList();
        r.ExitCompound();
    }

    /// <summary>
    /// Non-ASCII lookup-имя — café (BMP, 2-байт в mUTF-8). Проверка, что byte-compare работает и для
    /// non-ASCII (mUTF-8 = UTF-8 для BMP без \0). Крайне редкий кейс, но покрыть стоит.
    /// </summary>
    [Fact]
    public void TryReadInt_NonAsciiName_ByteCompareMatches()
    {
        // { Int("café", 99) } — "café" = 63 61 66 C3 A9 в mUTF-8 (= UTF-8 для BMP).
        ReadOnlySpan<byte> bytes = ParseHex(
            "0A"
            + " 03 00 05 63 61 66 C3 A9 00 00 00 63"     // Int("café", 99)
            + " 00");
        Span<NbtFrame> frames = stackalloc NbtFrame[8];
        var r = new NbtReader(bytes, frames);

        r.EnterRootCompound();
        // "café"u8 — это UTF-8 байты [63 61 66 C3 A9], что == mUTF-8 для café.
        Assert.True(r.TryReadInt("café"u8, out int value));
        Assert.Equal(99, value);

        r.ExitCompound();
        Assert.Equal(bytes.Length, r.Read);
    }

    // ─────────────────────  Массивы (round-trip writer→reader)  ─────────────────────

    /// <summary>
    /// ByteArray: zero-copy чтение — срез ссылается на буфер reader'а. Round-trip с WriteByteArray.
    /// </summary>
    [Fact]
    public void ByteArray_RoundTrip_ReturnsSameBytes()
    {
        // Пишем: root { ByteArray("data", [1,2,3]) }
        Span<byte> written = stackalloc byte[64];
        Span<NbtFrame> wframes = stackalloc NbtFrame[8];
        var w = new NbtWriter(written, wframes);
        w.BeginRootCompound()
            .WriteByteArray("data"u8, [1, 2, 3])
        .EndCompound();

        // Читаем.
        var r = new NbtReader(w.Finish(), wframes);   // переиспользуем frames-буфер
        r.EnterRootCompound();
        Assert.True(r.TryReadByteArray("data"u8, out ReadOnlySpan<byte> value));
        Assert.Equal(new byte[] { 1, 2, 3 }, value.ToArray());

        r.ExitCompound();
        Assert.Equal(w.Written, r.Read);
    }

    /// <summary>
    /// IntArray: BE → host-endian, в destination Span. Round-trip с WriteIntArray.
    /// </summary>
    [Fact]
    public void IntArray_RoundTrip_FillsDestinationSpan()
    {
        // Пишем: root { IntArray("ids", [1000001, 2000002, 3000003]) } — значения > byte, чтобы поймать endian-баг.
        Span<byte> written = stackalloc byte[64];
        Span<NbtFrame> wframes = stackalloc NbtFrame[8];
        var w = new NbtWriter(written, wframes);
        w.BeginRootCompound()
            .WriteIntArray("ids"u8, [1000001, 2000002, 3000003])
        .EndCompound();

        var r = new NbtReader(w.Finish(), wframes);
        r.EnterRootCompound();
        Span<int> dest = stackalloc int[8];   // больше, чем в потоке — лишнее не трогается
        Assert.True(r.TryReadIntArray("ids"u8, dest, out int count));
        Assert.Equal(3, count);
        Assert.Equal(1000001, dest[0]);
        Assert.Equal(2000002, dest[1]);
        Assert.Equal(3000003, dest[2]);

        r.ExitCompound();
        Assert.Equal(w.Written, r.Read);
    }

    /// <summary>
    /// LongArray: аналогично IntArray, 8-байтные значения. Round-trip с WriteLongArray.
    /// </summary>
    [Fact]
    public void LongArray_RoundTrip_FillsDestinationSpan()
    {
        Span<byte> written = stackalloc byte[64];
        Span<NbtFrame> wframes = stackalloc NbtFrame[8];
        var w = new NbtWriter(written, wframes);
        w.BeginRootCompound()
            .WriteLongArray("positions"u8, [0x0102030405060708L, -5L])
        .EndCompound();

        var r = new NbtReader(w.Finish(), wframes);
        r.EnterRootCompound();
        Span<long> dest = stackalloc long[4];
        Assert.True(r.TryReadLongArray("positions"u8, dest, out int count));
        Assert.Equal(2, count);
        Assert.Equal(0x0102030405060708L, dest[0]);
        Assert.Equal(-5L, dest[1]);

        r.ExitCompound();
        Assert.Equal(w.Written, r.Read);
    }

    /// <summary>
    /// Массивы в List: читаем безымянные перегрузки. Round-trip с writer'овскими безымянными перегрузками.
    /// </summary>
    [Fact]
    public void ByteArrayInList_RoundTrip_ReadsUnnamedOverload()
    {
        // Пишем: root { List("rows", ByteArray, 2, [[10],[20,30]]) }
        Span<byte> written = stackalloc byte[64];
        Span<NbtFrame> wframes = stackalloc NbtFrame[8];
        var w = new NbtWriter(written, wframes);
        w.BeginRootCompound()
            .BeginList("rows"u8, NbtTagType.ByteArray, 2)
            .WriteByteArray([10])
            .WriteByteArray([20, 30])
            .EndList()
        .EndCompound();

        var r = new NbtReader(w.Finish(), wframes);
        r.EnterRootCompound();
        r.ReadTagName(out var t, out _);
        Assert.Equal(NbtTagType.List, t);
        r.EnterList(out var elemType, out int count);
        Assert.Equal(NbtTagType.ByteArray, elemType);
        Assert.Equal(2, count);

        Assert.Equal(new byte[] { 10 }, r.ReadByteArray().ToArray());
        Assert.Equal(new byte[] { 20, 30 }, r.ReadByteArray().ToArray());

        r.ExitList();
        r.ExitCompound();
        Assert.Equal(w.Written, r.Read);
    }

    /// <summary>
    /// ByteArray lookup отсутствующего имени → false (zero-copy default), End не потребляется.
    /// Защита на то, что lookup-флаг работает для массивов так же, как для скаляров.
    /// </summary>
    [Fact]
    public void ByteArray_NotFound_ReturnsFalse()
    {
        // { Int("x", 1) }
        Span<byte> written = stackalloc byte[64];
        Span<NbtFrame> wframes = stackalloc NbtFrame[8];
        var w = new NbtWriter(written, wframes);

        w.BeginRootCompound()
            .WriteInt("x"u8, 1)
        .EndCompound();

        var r = new NbtReader(w.Finish(), wframes);
        r.EnterRootCompound();
        Assert.False(r.TryReadByteArray("missing"u8, out ReadOnlySpan<byte> value));
        Assert.True(value.IsEmpty);

        r.ExitCompound();
        Assert.Equal(w.Written, r.Read);
    }

    // ─────────────────────  Хелпер  ─────────────────────

    private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
}
