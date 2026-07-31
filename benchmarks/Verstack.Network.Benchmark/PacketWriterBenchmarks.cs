using System;
using BenchmarkDotNet.Attributes;
using Verstack.Network.Packet.Writers;

namespace Verstack.Network.Benchmarks;

// Включаем сбор статистики по памяти и аллокациям
[MemoryDiagnoser]
public class PacketWriterBenchmarks
{
    // Буфер, в который мы будем писать пакет (имитация outbound.PayloadBuffer)
    private byte[] _payloadBuffer = null!;
    
    // Данные для тестов
    private string _minecraftOverworldString = null!;
    private ReadOnlySpan<byte> _minecraftOverworldUtf8 => "minecraft:overworld"u8;
    private Guid _testUuid;

    [GlobalSetup]
    public void Setup()
    {
        _payloadBuffer = new byte[256];
        _minecraftOverworldString = "minecraft:overworld";
        _testUuid = Guid.NewGuid();
    }

    // ─────────────────────────  Тесты примитивов  ─────────────────────────

    [Benchmark(Baseline = true)]
    public int WriteVarInt()
    {
        var writer = new PacketWriter(_payloadBuffer);
        writer.WriteVarInt(2147483647); // Максимальный VarInt (5 байт)
        return writer.Written;
    }

    [Benchmark]
    public int WriteString_FromUtf8Span()
    {
        var writer = new PacketWriter(_payloadBuffer);
        writer.WriteString(_minecraftOverworldUtf8);
        return writer.Written;
    }

    [Benchmark]
    public int WriteString_FromCSharpString()
    {
        var writer = new PacketWriter(_payloadBuffer);
        writer.WriteString(_minecraftOverworldString);
        return writer.Written;
    }

    [Benchmark]
    public int WriteUuid()
    {
        var writer = new PacketWriter(_payloadBuffer);
        writer.WriteUuid(_testUuid);
        return writer.Written;
    }

    [Benchmark]
    public int WriteVector3()
    {
        var writer = new PacketWriter(_payloadBuffer);
        writer.WriteVector3(10, 64, -20);
        return writer.Written;
    }

    // ─────────────────────  Комплексный тест (Сборка пакета)  ─────────────────────

    [Benchmark]
    public int AssemblePlayLoginPacket()
    {
        var writer = new PacketWriter(_payloadBuffer);
        
        writer.WriteVarInt(0x31)                        // Packet ID
              .WriteInt(1)                              // Entity ID
              .WriteBool(false)                         // Is Hardcore
              .WriteVarInt(1)                           // Dimension Count
              .WriteString(_minecraftOverworldUtf8)     // Dimension Names
              .WriteVarInt(20)                          // Max Players
              .WriteVarInt(10)                          // View Distance
              .WriteVarInt(10)                          // Simulation Distance
              .WriteBool(false)                         // Reduced Debug Info
              .WriteBool(true)                          // Enable Respawn Screen
              .WriteBool(false)                         // Do Limited Crafting
              .WriteVarInt(0)                           // Dimension Type
              .WriteString(_minecraftOverworldUtf8)     // Dimension
              .WriteLong(0)                             // Seed
              .WriteByteRaw(1)                          // Game Mode (Creative)
              .WriteByteRaw(0xFF)                       // Previous Game Mode
              .WriteBool(false)                         // Is Debug
              .WriteBool(false)                         // Is Flat
              .WriteBool(false)                         // Has Death Location
              .WriteVarInt(0)                           // Portal Cooldown
              .WriteVarInt(63)                          // Sea Level
              .WriteBool(false)                         // Online Mode
              .WriteBool(false);                        // Enforces Secure Chat

        return writer.Written;
    }
}