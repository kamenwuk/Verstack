using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Shared.Voxel;
using System.Buffers;

namespace Verstack.Layers.Realm.Chunks;

/// <summary>
/// Сборка тела пакета level_chunk_with_light (0x2D) протокола 776.
/// Секции/контейнеры сериализует сам Shared.Voxel (WriteTo в Span); здесь только
/// верхний уровень: X/Z, Heightmaps, Size + Data, Block Entities, Light.
/// </summary>
public readonly ref struct ChunkWireWriter
{
    private const int MAX_SECTION_SIZE = 2 + 2 + 9226 + 876;

    public void Write(ref PacketStreamWriter writer, ChunkColumn column, int chunkX, int chunkZ)
    {
        writer.WriteInt(chunkX).WriteInt(chunkZ);
        WriteHeightmaps(ref writer, column);

        // Секции пишем во временный буфер, чтобы измерить Size до записи.
        var sectionCount = column.SectionCount;
        var buffer = ArrayPool<byte>.Shared.Rent(MAX_SECTION_SIZE * sectionCount);
        try
        {
            var written = 0;
            for (int i = 0; i < sectionCount; i++)
            {
                ref var section = ref column.GetSectionByIndex(i);
                written += section.WriteTo(buffer.AsSpan(written));
            }
            writer.WriteVarInt(written);
            writer.WriteSpan(buffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        WriteBlockEntities(ref writer);
        WriteLight(ref writer, sectionCount);
    }

    private static void WriteHeightmaps(ref PacketStreamWriter writer, ChunkColumn column)
    {
        writer.WriteVarInt(2);
        WriteHeightmap(ref writer, 4, column.MotionBlocking.RawData);
        WriteHeightmap(ref writer, 1, column.WorldSurface.RawData);
    }

    private static void WriteHeightmap(ref PacketStreamWriter writer, int type, ReadOnlySpan<long> data)
    {
        writer.WriteVarInt(type).WriteVarInt(data.Length);
        for (int i = 0; i < data.Length; i++)
            writer.WriteLong(data[i]);
    }

    private static void WriteBlockEntities(ref PacketStreamWriter writer)
        => writer.WriteVarInt(0);

    // Заглушка: пустой light. Клиент не крашнется, но будет тьма.
    // Полная реализация (sky/block propagation) — отдельная задача.
    private static void WriteLight(ref PacketStreamWriter writer, int sectionCount)
    {
        writer.WriteVarInt(0).WriteVarInt(0).WriteVarInt(0).WriteVarInt(0).WriteVarInt(0).WriteVarInt(0);
    }
}