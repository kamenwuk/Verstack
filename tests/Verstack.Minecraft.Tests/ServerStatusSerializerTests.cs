using System.Buffers;
using System.Text.Json;
using Verstack.Minecraft.Status;
using Verstack.Protocol;

namespace Verstack.Minecraft.Tests;

/// <summary>
/// Tests for ServerStatusSerializer: payload structure
/// ([packetId][jsonLen][JSON]), JSON content via JsonDocument,
/// field values, empty MOTD, large player counts.
/// Driven through ArrayBufferWriter&lt;byte&gt; (no socket, no pipe).
/// </summary>
public class ServerStatusSerializerTests
{
    private static readonly ServerStatusResponse SampleStatus = new(
        new ServerVersion("1.21.6", 774),
        new ServerCapacity(max: 20, online: 0),
        "A Minecraft Server");

    // ─── Структура payload ─────────────────────────────────────────

    [Fact]
    public void Write_PayloadStartsWithPacketIdZero()
    {
        // [VarInt(PACKET_ID=0x00)] — первый байт payload обязан быть 0x00.
        var writer = new ArrayBufferWriter<byte>();

        ServerStatusSerializer.Write(writer, SampleStatus);

        Assert.Equal(ServerStatusSerializer.PACKET_ID, writer.WrittenSpan[0]);
    }

    [Fact]
    public void Write_PayloadStructure_PacketIdThenJsonLenThenJson()
    {
        // Полная структура: [VarInt(packetId)] [VarInt(jsonLen)] [JSON].
        // Длина JSON в VarInt обязана совпадать с реальным числом байт тела.
        var writer = new ArrayBufferWriter<byte>();
        ServerStatusSerializer.Write(writer, SampleStatus);

        ReadOnlySpan<byte> payload = writer.WrittenSpan;

        // packet ID
        Assert.True(VarInt.TryDecode(payload, out int packetId, out int idBytes));
        Assert.Equal(ServerStatusSerializer.PACKET_ID, packetId);

        // длина JSON
        ReadOnlySpan<byte> afterId = payload[idBytes..];
        Assert.True(VarInt.TryDecode(afterId, out int jsonLen, out int lenBytes));

        // тело: заявленная длина совпадает с фактической
        ReadOnlySpan<byte> jsonBytes = afterId[lenBytes..];
        Assert.Equal(jsonLen, jsonBytes.Length);
    }

    // ─── Содержимое JSON ───────────────────────────────────────────

    [Fact]
    public void Write_Json_VersionFieldsCorrect()
    {
        JsonElement root = ParseJson(out _);

        JsonElement version = root.GetProperty("version");
        Assert.Equal("1.21.6", version.GetProperty("name").GetString());
        Assert.Equal(774, version.GetProperty("protocol").GetInt32());
    }

    [Fact]
    public void Write_Json_PlayerFieldsCorrect()
    {
        JsonElement root = ParseJson(out _);

        JsonElement players = root.GetProperty("players");
        Assert.Equal(20, players.GetProperty("max").GetInt32());
        Assert.Equal(0, players.GetProperty("online").GetInt32());
    }

    [Fact]
    public void Write_Json_DescriptionCorrect()
    {
        JsonElement root = ParseJson(out _);

        Assert.Equal("A Minecraft Server", root.GetProperty("description").GetProperty("text").GetString());
    }

    // ─── Corner cases ──────────────────────────────────────────────

    [Fact]
    public void Write_EmptyDescription_StillValidJson()
    {
        // Пустой MOTD: description.text = "". Corner case для WriteString.
        var status = new ServerStatusResponse(
            new ServerVersion("1.21.6", 774),
            new ServerCapacity(max: 20, online: 0),
            "");

        var writer = new ArrayBufferWriter<byte>();
        ServerStatusSerializer.Write(writer, status);

        JsonElement root = ParseJsonFromMemory(writer.WrittenMemory, out _);
        Assert.Equal("", root.GetProperty("description").GetProperty("text").GetString());
    }

    [Fact]
    public void Write_LargePlayerCount_NumbersPreserved()
    {
        // Большие значения max/online: проверка, что Utf8JsonWriter корректно
        // кодирует числа любой величины — кадр не разваливается.
        var status = new ServerStatusResponse(
            new ServerVersion("1.21.6", 774),
            new ServerCapacity(max: 1000, online: 999),
            "Test");

        var writer = new ArrayBufferWriter<byte>();
        ServerStatusSerializer.Write(writer, status);

        JsonElement root = ParseJsonFromMemory(writer.WrittenMemory, out _);
        JsonElement players = root.GetProperty("players");
        Assert.Equal(1000, players.GetProperty("max").GetInt32());
        Assert.Equal(999, players.GetProperty("online").GetInt32());
    }

    /// <summary>Сериализует SampleStatus и парсит JSON-тело payload.</summary>
    private JsonElement ParseJson(out int packetId)
    {
        var writer = new ArrayBufferWriter<byte>();
        ServerStatusSerializer.Write(writer, SampleStatus);
        return ParseJsonFromMemory(writer.WrittenMemory, out packetId);
    }

    /// <summary>
    /// Пропускает [VarInt(packetId)] [VarInt(jsonLen)] и парсит оставшееся
    /// тело как JSON. Clone() обязателен — иначе JsonDocument.Dispose()
    /// инвалидирует возвращённый элемент.
    /// </summary>
    private static JsonElement ParseJsonFromMemory(ReadOnlyMemory<byte> payload, out int packetId)
    {
        // VarInt-декодинг идёт по span — там ничего хранить не надо.
        ReadOnlySpan<byte> payloadSpan = payload.Span;
        Assert.True(VarInt.TryDecode(payloadSpan, out packetId, out int idBytes));
        ReadOnlySpan<byte> afterId = payloadSpan[idBytes..];
        Assert.True(VarInt.TryDecode(afterId, out int jsonLen, out int lenBytes));

        // А вот JSON-тело отдаём как Memory — JsonDocument.Parse хранит буфер.
        ReadOnlyMemory<byte> jsonMemory = payload.Slice(idBytes + lenBytes);
        Assert.Equal(jsonLen, jsonMemory.Length);

        using var doc = JsonDocument.Parse(jsonMemory);
        return doc.RootElement.Clone();
    }
}