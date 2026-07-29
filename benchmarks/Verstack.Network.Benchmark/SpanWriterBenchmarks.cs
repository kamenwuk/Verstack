using System;
using BenchmarkDotNet.Attributes;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

[ShortRunJob]
[MemoryDiagnoser]
public class SpanWriterBenchmarks
{
    private byte[] _buffer;
    private const int BufferSize = 4096;

    [GlobalSetup]
    public void Setup() => _buffer = new byte[BufferSize];

    [Benchmark]
    public int WriteVarInt()
    {
        var writer = new SpanWriter(_buffer);
        VarInt.Write(ref writer, 123456789);
        return writer.Written;
    }

    [Benchmark]
    public int WriteUtf8String()
    {
        var writer = new SpanWriter(_buffer);
        Utf8String.Write(ref writer, "minecraft:stone");
        return writer.Written;
    }

    [Benchmark]
    public int WriteUtf8Bytes()
    {
        var writer = new SpanWriter(_buffer);
        ReadOnlySpan<byte> bytes = System.Text.Encoding.UTF8.GetBytes("minecraft:stone");
        Utf8String.Write(ref writer, bytes);
        return writer.Written;
    }
}