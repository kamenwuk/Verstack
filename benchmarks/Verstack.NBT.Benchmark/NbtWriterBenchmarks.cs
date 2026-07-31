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
        return writer.Written;
    }

    [Benchmark]
    public int WriteCompound_Nested()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginCompound("inner"u8);
        writer.WriteInt("x"u8, 10);
        writer.WriteInt("y"u8, 20);
        writer.BeginCompound("deep"u8);
        writer.WriteString("key"u8, "value"u8);
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
        writer.BeginList("numbers"u8, NbtTagType.Int, 100);
        for (int i = 0; i < 100; i++)
            writer.WriteListItemInt(i);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }

    [Benchmark]
    public int WriteList_OfStrings()
    {
        var writer = new NbtWriter(_buffer, _frames, Networked);
        writer.BeginRootCompound();
        writer.BeginList("words"u8, NbtTagType.String, 20);
        writer.WriteListItemString("alpha"u8);
        writer.WriteListItemString("beta"u8);
        writer.WriteListItemString("gamma"u8);
        writer.WriteListItemString("delta"u8);
        writer.WriteListItemString("epsilon"u8);
        for (int i = 0; i < 15; i++)
            writer.WriteListItemString("filler"u8);
        writer.EndList();
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
        writer.BeginList("data"u8, NbtTagType.Byte, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteListItemByte((sbyte)(i & 0xFF));
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
        writer.BeginList("ints"u8, NbtTagType.Int, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteListItemInt(i);
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
            writer.BeginCompound("level"u8);
        writer.WriteInt("value"u8, 42);
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
        writer.BeginCompound("emptyCompound"u8);
        writer.EndCompound();
        writer.BeginList("emptyList"u8, NbtTagType.Byte, 0);
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
            writer.WriteInt("i"u8, i);
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
        writer.BeginList("doubles"u8, NbtTagType.Double, 1000);
        for (int i = 0; i < 1000; i++)
            writer.WriteListItemDouble(i * 0.1);
        writer.EndList();
        writer.EndCompound();
        return writer.Written;
    }
}