using System;
using BenchmarkDotNet.Attributes;
using Verstack.Nbt;

[ShortRunJob]
[MemoryDiagnoser]
public class NbtWriterBenchmarks
{
    private byte[] _buffer = null!;
    private NbtFrame[] _frames = null!;
    private const int MAX_DEPTH = 64;

    [Params(true, false)]
    public bool Networked { get; set; }

    // Предаллоцированные данные для тестов, чтобы не мерить аллокации тестовых строк
    private string _bigString = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new byte[65536];  // достаточно для любых тестов
        _frames = new NbtFrame[MAX_DEPTH];
        _bigString = new string('A', 2000);
    }

    // ---------- Простые сценарии ----------

    [Benchmark]
    public int WriteCompound_SimplePrimitives()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.WriteByte("byte", 127);
        writer.WriteShort("short", 32000);
        writer.WriteInt("int", 42);
        writer.WriteLong("long", long.MaxValue);
        writer.WriteFloat("float", 3.14f);
        writer.WriteDouble("double", 2.718281828);
        writer.WriteString("str", "Hello, NBT!");
        writer.WriteBool("flag", true);
        writer.EndCompound();
        return writer.Written;
    }

    [Benchmark]
    public int WriteCompound_Nested()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginCompound("inner");
        writer.WriteInt("x", 10);
        writer.WriteInt("y", 20);
        writer.BeginCompound("deep");
        writer.WriteString("key", "value");
        writer.EndCompound();
        writer.EndCompound();
        writer.EndCompound();
        return writer.Written;
    }

    [Benchmark]
    public int WriteList_100_Ints()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("numbers", NbtTagType.Int, 100);
        for (int i = 0; i < 100; i++)
            writer.WriteInt(i);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    [Benchmark]
    public int WriteList_OfStrings()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("words", NbtTagType.String, 20);
        writer.WriteString("alpha");
        writer.WriteString("beta");
        writer.WriteString("gamma");
        writer.WriteString("delta");
        writer.WriteString("epsilon");
        for (int i = 0; i < 15; i++)
            writer.WriteString("filler");
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    [Benchmark]
    public int WriteLargeString()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.WriteString("big", _bigString);
        writer.EndCompound();
        return writer.Written;
    }

    // ---------- Массивы через примитивы (без расширений) ----------

    /// <summary>ByteArray как список отдельных байт (1000 элементов)</summary>
    [Benchmark]
    public int WriteByteArray_ViaList()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("data", NbtTagType.Byte, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteByte((sbyte)(i & 0xFF));
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    /// <summary>IntArray как список int (1000 элементов)</summary>
    [Benchmark]
    public int WriteIntArray_ViaList()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("ints", NbtTagType.Int, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteInt(i);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    // ---------- Продвинутые сценарии ----------

    /// <summary>Глубокая вложенность (10 уровней Compound)</summary>
    [Benchmark]
    public int WriteDeepCompound()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        for (int i = 0; i < 10; i++)
            writer.BeginCompound("level");
        writer.WriteInt("value", 42);
        for (int i = 0; i < 10; i++)
            writer.EndCompound();
        writer.EndCompound();
        return writer.Written;
    }

    /// <summary>Пустой Compound и пустой List</summary>
    [Benchmark]
    public int WriteEmptyCompoundAndList()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginCompound("emptyCompound");
        writer.EndCompound();
        writer.BeginList("emptyList", NbtTagType.Byte, 0);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    /// <summary>Многократное переиспользование буфера (100 итераций)</summary>
    [Benchmark]
    public int RepeatedUse()
    {
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            var writer = new NbtWriter(_buffer, _frames, Networked);
            writer.BeginRootCompound();
            writer.WriteInt("i", i);
            writer.EndCompound();
            total += writer.Written;
        }
        return total;
    }

    /// <summary>Запись списка из 1000 double</summary>
    [Benchmark]
    public int WriteList_1000_Doubles()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("doubles", NbtTagType.Double, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteDouble(i * 0.1);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }
}