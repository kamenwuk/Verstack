using Verstack.Protocol;
using System.Text.Json;
using System.Buffers;

namespace Verstack.Minecraft.Status;

/// <summary>
/// Serializes a <see cref="ServerStatusResponse"/> into a Status Response payload
/// (client-bound packet <see cref="PACKET_ID"/> in the Status state).
/// </summary>
/// <remarks>
/// Produces the packet PAYLOAD only —
/// <c>[VarInt(PACKET_ID)][VarInt(jsonLen)][UTF-8 JSON]</c>. Framing (the outer
/// length prefix) is <see cref="PacketFraming"/>'s job; the caller composes the
/// two. This mirrors the read side, where <see cref="PacketFrameScanner"/>
/// yields payloads and a (future) dispatcher parses them.
/// </remarks>
public static class ServerStatusSerializer
{
    /// <summary>Status Response packet id in the Status state.</summary>
    public const int PACKET_ID = 0x00;

    /// <summary>
    /// Writes the Status Response payload to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Destination buffer — typically a scratch buffer the
    /// caller then frames via <see cref="PacketFraming"/>.</param>
    /// <param name="status">Server status data to serialize.</param>
    public static void Write(IBufferWriter<byte> output, in ServerStatusResponse status)
    {
        // Фаза 1: JSON во временный буфер. Длина тела неизвестна до записи,
        // значит префикс длины можно поставить только после. Utf8JsonWriter
        // не умеет «dry-run», поэтому сериализуем целиком и замеряем длину.
        // Одна аллокация на статус — допустимо для не-горячего пути пинга.
        // (Будущее: пулить буфер через ArrayPool.)
        var jsonScratch = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(jsonScratch))
        {
            WriteJson(json, in status);
        } // dispose фиксирует (flush) байты в jsonScratch

        ReadOnlySpan<byte> jsonBytes = jsonScratch.WrittenSpan;
        int jsonLen = jsonBytes.Length;

        // Фаза 2: payload одним слитным куском.
        // [VarInt(PACKET_ID)] [VarInt(jsonLen)] [JSON]
        Span<byte> span = output.GetSpan(VarInt.MAX_SIZE + VarInt.MAX_SIZE + jsonLen);
        int offset = 0;
        offset += VarInt.Encode(PACKET_ID, span[offset..]);
        offset += VarInt.Encode(jsonLen, span[offset..]);
        jsonBytes.CopyTo(span[offset..]);
        output.Advance(offset + jsonLen);
    }

    private static void WriteJson(Utf8JsonWriter json, in ServerStatusResponse status)
    {
        json.WriteStartObject();

        json.WriteStartObject("version");
        json.WriteString("name", status.Version.Name);
        json.WriteNumber("protocol", status.Version.Protocol);
        json.WriteEndObject();

        json.WriteStartObject("players");
        json.WriteNumber("max", status.Capacity.Max);
        json.WriteNumber("online", status.Capacity.Online);
        json.WriteEndObject();

        json.WriteStartObject("description");
        json.WriteString("text", status.Description);
        json.WriteEndObject();

        json.WriteEndObject();
    }
}