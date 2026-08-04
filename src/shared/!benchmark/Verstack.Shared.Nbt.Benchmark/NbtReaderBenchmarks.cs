using BenchmarkDotNet.Attributes;
using Verstack.Shared.Nbt.Reader;
using Verstack.Shared.Nbt.Writer;

namespace Verstack.Shared.Nbt.Benchmark;

[ShortRunJob]
[MemoryDiagnoser]
public class NbtReaderBenchmarks
{
    private byte[] _simpleCompoundData = null!;
    private byte[] _nestedCompoundData = null!;
    private byte[] _list100IntsData = null!;
    private byte[] _listOfStringsData = null!;
    private byte[] _largeStringData = null!;
    private byte[] _byteArrayViaListData = null!;
    private byte[] _intArrayViaListData = null!;
    private byte[] _deepCompoundData = null!;
    private byte[] _emptyCompoundAndListData = null!;

    private NbtFrame[] _frames = null!;
    private char[] _stringBuf = null!;   // переиспользуемый буфер под ReadString(Span<char>) — zero-alloc в итерации
    private const int MaxDepth = 64;

    [Params(true, false)]
    public bool Networked { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _frames = new NbtFrame[MaxDepth];
        _stringBuf = new char[4096];   // общий буфер под строковые значения — переиспользуется между итерациями

        _simpleCompoundData = GenerateSimpleCompound(Networked);
        _nestedCompoundData = GenerateNestedCompound(Networked);
        _list100IntsData = GenerateList100Ints(Networked);
        _listOfStringsData = GenerateListOfStrings(Networked);
        _largeStringData = GenerateLargeString(Networked);
        _byteArrayViaListData = GenerateByteArrayViaList(Networked);
        _intArrayViaListData = GenerateIntArrayViaList(Networked);
        _deepCompoundData = GenerateDeepCompound(Networked);
        _emptyCompoundAndListData = GenerateEmptyCompoundAndList(Networked);
    }

    // Генераторы данных (используют NbtWriter)
    private static byte[] GenerateSimpleCompound(bool networked)
    {
        Span<byte> buf = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound()
            .WriteByte("byte"u8, 127)
            .WriteShort("short"u8, 32000)
            .WriteInt("int"u8, 42)
            .WriteLong("long"u8, long.MaxValue)
            .WriteFloat("float"u8, 3.14f)
            .WriteDouble("double"u8, 2.718281828)
            .WriteString("str"u8, "Hello, NBT!"u8)
            .WriteBool("flag"u8, true)
        .EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateNestedCompound(bool networked)
    {
        Span<byte> buf = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound()
            .BeginCompound("inner"u8)
                .WriteInt("x"u8, 10)
                .WriteInt("y"u8, 20)
            .BeginCompound("deep"u8)
            .WriteString("key"u8, "value"u8)
            .EndCompound()
            .EndCompound()
        .EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateList100Ints(bool networked)
    {
        Span<byte> buf = stackalloc byte[1024];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.BeginList("numbers"u8, NbtTagType.Int, 100);
        for (int i = 0; i < 100; i++) writer.WriteListItemInt(i);
        writer.EndList();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateListOfStrings(bool networked)
    {
        Span<byte> buf = stackalloc byte[1024];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.BeginList("words"u8, NbtTagType.String, 20);
        writer.WriteListItemString("alpha"u8); writer.WriteListItemString("beta"u8); writer.WriteListItemString("gamma"u8);
        writer.WriteListItemString("delta"u8); writer.WriteListItemString("epsilon"u8);
        for (int i = 0; i < 15; i++) writer.WriteListItemString("filler"u8);
        writer.EndList();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateLargeString(bool networked)
    {
        Span<byte> buf = stackalloc byte[4096];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.WriteString("big"u8, "bigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbibigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbigbiggbigbigbigbigbig"u8);
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateByteArrayViaList(bool networked)
    {
        Span<byte> buf = new byte[2000]; // достаточно для 1000 байт
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.BeginList("data"u8, NbtTagType.Byte, 1000);
        for (int i = 0; i < 1000; i++) writer.WriteListItemByte((sbyte)(i & 0xFF));
        writer.EndList();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateIntArrayViaList(bool networked)
    {
        Span<byte> buf = new byte[5000]; // 1000 * 4 + заголовки
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.BeginList("ints"u8, NbtTagType.Int, 1000);
        for (int i = 0; i < 1000; i++) writer.WriteListItemInt(i);
        writer.EndList();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateDeepCompound(bool networked)
    {
        Span<byte> buf = stackalloc byte[256];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        for (int i = 0; i < 10; i++) writer.BeginCompound("level"u8);
        writer.WriteInt("value"u8, 42);
        for (int i = 0; i < 10; i++) writer.EndCompound();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    private static byte[] GenerateEmptyCompoundAndList(bool networked)
    {
        Span<byte> buf = stackalloc byte[128];
        Span<NbtFrame> frames = stackalloc NbtFrame[MaxDepth];
        var writer = new NbtStreamWriter(buf, frames, networked);
        writer.BeginRootCompound();
        writer.BeginCompound("emptyCompound"u8);
        writer.EndCompound();
        writer.BeginList("emptyList"u8, NbtTagType.Byte, 0);
        writer.EndList();
        writer.EndCompound();
        return buf[..writer.Written].ToArray();
    }

    // --- Бенчмарки (zero-alloc: byte-literal имена + Span<char> для строковых значений) ---
    [Benchmark]
    public int ReadCompound_SimplePrimitives()
    {
        var reader = new NbtStreamReader(_simpleCompoundData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryReadByte("byte"u8, out sbyte b);
        reader.TryReadShort("short"u8, out short s);
        reader.TryReadInt("int"u8, out int i);
        reader.TryReadLong("long"u8, out long l);
        reader.TryReadFloat("float"u8, out float f);
        reader.TryReadDouble("double"u8, out double d);
        reader.TryReadString("str"u8, _stringBuf, out int strLen);
        reader.TryReadBool("flag"u8, out bool flag);
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + b + s + i + (int)l + (int)f + (int)d + strLen + (flag ? 1 : 0);
    }

    [Benchmark]
    public int ReadCompound_Nested()
    {
        var reader = new NbtStreamReader(_nestedCompoundData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryEnterCompound("inner"u8);
        reader.TryReadInt("x"u8, out int x);
        reader.TryReadInt("y"u8, out int y);
        reader.TryEnterCompound("deep"u8);
        reader.TryReadString("key"u8, _stringBuf, out int keyLen);
        reader.ExitCompound();
        reader.ExitCompound();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + x + y + keyLen;
    }

    [Benchmark]
    public int ReadList_100_Ints()
    {
        var reader = new NbtStreamReader(_list100IntsData, _frames, Networked);
        reader.EnterRootCompound();
        bool ok = reader.TryEnterList("numbers"u8, out _, out int count);
        if (!ok) throw new Exception();
        int sum = 0;
        for (int i = 0; i < count; i++) sum += reader.ReadInt();
        reader.ExitList();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + sum;
    }

    [Benchmark]
    public int ReadList_OfStrings()
    {
        var reader = new NbtStreamReader(_listOfStringsData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryEnterList("words"u8, out _, out int count);
        int len = 0;
        for (int i = 0; i < count; i++)
        {
            reader.ReadString(_stringBuf, out int charsWritten);
            len += charsWritten;
        }
        reader.ExitList();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + len;
    }

    [Benchmark]
    public int ReadLargeString()
    {
        var reader = new NbtStreamReader(_largeStringData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryReadString("big"u8, _stringBuf, out int bigLen);
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + bigLen;
    }

    [Benchmark]
    public int ReadByteArray_ViaList()
    {
        var reader = new NbtStreamReader(_byteArrayViaListData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryEnterList("data"u8, out _, out int count);
        int sum = 0;
        for (int i = 0; i < count; i++) sum += reader.ReadByte();
        reader.ExitList();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + sum;
    }

    [Benchmark]
    public int ReadIntArray_ViaList()
    {
        var reader = new NbtStreamReader(_intArrayViaListData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryEnterList("ints"u8, out _, out int count);
        int sum = 0;
        for (int i = 0; i < count; i++) sum += reader.ReadInt();
        reader.ExitList();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + sum;
    }

    [Benchmark]
    public int ReadDeepCompound()
    {
        var reader = new NbtStreamReader(_deepCompoundData, _frames, Networked);
        reader.EnterRootCompound();
        for (int i = 0; i < 10; i++)
            reader.TryEnterCompound("level"u8);
        reader.TryReadInt("value"u8, out int val);
        for (int i = 0; i < 10; i++)
            reader.ExitCompound();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + val;
    }

    [Benchmark]
    public int ReadEmptyCompoundAndList()
    {
        var reader = new NbtStreamReader(_emptyCompoundAndListData, _frames, Networked);
        reader.EnterRootCompound();
        reader.TryEnterCompound("emptyCompound"u8);
        reader.ExitCompound();
        reader.TryEnterList("emptyList"u8, out _, out int count);
        reader.ExitList();
        reader.SkipRemaining();
        reader.ExitCompound();
        return reader.Read + count;
    }

    [Benchmark]
    public int RepeatedUse_Read()
    {
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            var reader = new NbtStreamReader(_simpleCompoundData, _frames, Networked);
            reader.EnterRootCompound();
            reader.TryReadInt("int"u8, out int val);
            reader.SkipRemaining();
            reader.ExitCompound();
            total += reader.Read + val;
        }
        return total;
    }
}