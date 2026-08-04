using Verstack.Engine.Network.Packet.Readers;
using Verstack.Engine.Network.Packet.Writers;
using Verstack.Engine.Network.Packet;
using BenchmarkDotNet.Attributes;
using System.Buffers;

namespace Verstack.Engine.Network.Benchmark;

[MemoryDiagnoser]
public class PacketReaderBenchmarks
{
    private byte[] _buffer = null!;
    private byte[] _smallBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Готовим буфер с записанными данными
        var writer = new PacketStreamWriter(ArrayPool<byte>.Shared.Rent(256));
        writer.WriteVarInt(2147483647);
        writer.WriteString("minecraft:overworld"u8);
        writer.WriteUuid(Guid.NewGuid());
        writer.WriteVector3(10, 64, -20);
        _buffer = writer.WrittenSpan.ToArray();

        // Маленький буфер для теста Fault State
        _smallBuffer = new byte[] { 0x01, 0x02 };
    }

    [Benchmark(Baseline = true)]
    public int ReadVarInt()
    {
        var reader = new PacketStreamReader(_buffer, 5); // Только VarInt
        return reader.ReadVarInt();
    }

    /// <summary>
    /// Проверяет чтение строки и её сравнение с ожидаемой (0 аллокаций).
    /// </summary>
    [Benchmark]
    public bool ReadString_Compare()
    {
        var reader = new PacketStreamReader(_buffer, _buffer.Length);
        reader.ReadVarInt(); // Пропускаем VarInt
        ReadOnlyUtf8Span str = reader.ReadString();
        return str.Equals("minecraft:overworld"u8);
    }

    /// <summary>
    /// Проверяет чтение строки и её конвертацию в C# string (1 аллокация).
    /// </summary>
    [Benchmark]
    public string ReadString_Allocate()
    {
        var reader = new PacketStreamReader(_buffer, _buffer.Length);
        reader.ReadVarInt();
        ReadOnlyUtf8Span str = reader.ReadString();
        return str.ToString();
    }

    [Benchmark]
    public Guid ReadUuid()
    {
        var reader = new PacketStreamReader(_buffer, _buffer.Length);
        reader.ReadVarInt(); // Пропускаем
        reader.ReadString(); // Пропускаем
        return reader.ReadUuid();
    }

    [Benchmark]
    public (int x, int y, int z) ReadVector3()
    {
        var reader = new PacketStreamReader(_buffer, _buffer.Length);
        reader.ReadVarInt();
        reader.ReadString();
        reader.ReadUuid();
        return reader.ReadVector3();
    }

    [Benchmark]
    public void Reader_FaultedRead()
    {
        var reader = new PacketStreamReader(_smallBuffer, _smallBuffer.Length);
        reader.ReadLong(); // Вызовет Fault
        reader.ReadString(); // Скип
        reader.ReadVector3(); // Скип
    }
}