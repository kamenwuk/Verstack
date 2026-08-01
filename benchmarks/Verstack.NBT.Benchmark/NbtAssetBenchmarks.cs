using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Verstack.Nbt.Assets;
using Verstack.Nbt.Writer;
using System.Text.Json;
using Verstack.Nbt;

namespace Verstack.NBT.Benchmark;

[MemoryDiagnoser] // Показывает аллокации памяти (GC)
[Orderer(SummaryOrderPolicy.Method)]
[ShortRunJob]
[RankColumn]
public class NbtAssetBenchmarks
{
    private string _nbtFilePath = null!;
    private string _jsonString = null!;
    private JsonDocument _jsonDoc = null!;
    private CachedNbtBuffer _cachedBuffer = null!;

    // Параметры для бенчмарков файловых операций
    [Params(128, 1024, 8192)] 
    public int FileSizeBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // 1. Подготавливаем достаточно большой JSON, чтобы бенчмарк был репрезентативным
        _jsonString = GenerateComplexJson(100); 
        _jsonDoc = JsonDocument.Parse(_jsonString);

        // 2. Подготавливаем .nbt файл на диске для тестов чтения
        _nbtFilePath = Path.GetTempFileName();
        
        Span<NbtFrame> frames = stackalloc NbtFrame[64];
        Span<byte> buffer = stackalloc byte[8192];
        var writer = new NbtWriter(buffer, frames, networked: true);
        writer.WriteJsonRoot(_jsonDoc.RootElement);
        File.WriteAllBytes(_nbtFilePath, writer.Finish().ToArray());

        // 3. Загружаем Cached буфер
        _cachedBuffer = new CachedNbtBuffer();
        _cachedBuffer.Load(_nbtFilePath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _jsonDoc.Dispose();
        if (File.Exists(_nbtFilePath)) File.Delete(_nbtFilePath);
        _cachedBuffer.Unload();
    }

    // ───────────────  Бенчмарки  ───────────────

    /// <summary>
    /// Тестирует скорость конвертации JSON в NBT байты (в памяти).
    /// </summary>
    [Benchmark(Description = "JSON -> NBT (Compile)")]
    public int CompileJsonToNbt()
    {
        Span<NbtFrame> frames = stackalloc NbtFrame[64];
        Span<byte> buffer = stackalloc byte[8192];

        var writer = new NbtWriter(buffer, frames, networked: true);
        writer.WriteJsonRoot(_jsonDoc.RootElement);
        
        return writer.Finish().Length;
    }

    /// <summary>
    /// Тестирует скорость чтения .nbt файла с диска через ArrayPool.
    /// </summary>
    [Benchmark(Description = "Read Scoped (Disk)")]
    public int ReadScopedFromDisk()
    {
        using var scopedBuffer = ScopedNbtBuffer.Load(_nbtFilePath);
        return scopedBuffer.Data.Length;
    }

    /// <summary>
    /// Тестирует скорость получения NBT из памяти (без диска).
    /// </summary>
    [Benchmark(Description = "Read Cached (Memory)")]
    public byte ReadCachedFromMemory()
    {
        // Заставляем компилятор реально прочитать первый байт из памяти,
        // чтобы он не вырезал этот код как "unused".
        return _cachedBuffer.Data.Span[0];
    }

    // ───────────────  Вспомогательная логика  ───────────────

    private string GenerateComplexJson(int nestedObjectsCount)
    {
        using var stream = new StringWriter();
        stream.Write("{\"root\":\"overworld\",\"values\":[1,2,3,4,5],\"nested\":{");
        
        for (int i = 0; i < nestedObjectsCount; i++)
        {
            if (i > 0) stream.Write(",");
            stream.Write($"\"obj_{i}\":{{\"id\":{i},\"name\":\"item_{i}\",\"active\":true}}");
        }
        
        stream.Write("}}");
        return stream.ToString();
    }
}