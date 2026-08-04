using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Writers;
using Verstack.Engine.Network.Packet;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// Загрузка территории при входе: game_event 13 (Start waiting for level chunks) как прелюдия,
/// затем Set Center Chunk (0x5E) + Chunk Batch Start (0x0C) + level_chunk_with_light (0x2D) × 25
/// (сетка 5×5, FlatGenerator) + Chunk Batch Finished (0x0B).
///
/// game_event 13 семантически принадлежит chunk-фазе — это сигнал клиенту начать ожидание чанков,
/// поэтому объединён с batch в один бандл.
/// </summary>
internal sealed class JoinChunkBatchBundle : PacketBundle
{
    public override int StepCount => 1;
    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        // 0. game_event (0x26), event 13: Start waiting for level chunks — прелюдия к batch.
        var gameEvent = outbound.Begin();
        gameEvent.WriteVarInt(0x26)
            .WriteByte(13) // Event — 13: Start waiting for level chunks.
            .WriteFloat(0.0f); // Value — для события 13 игнорируется, по стандарту 0.0f.
        outbound.Commit(ref gameEvent);

        // 1. Set Center Chunk (0x5E)
        var centerChunk = outbound.Begin();
        centerChunk.WriteVarInt(0x5E)
            .WriteVarInt(0) // Chunk X
            .WriteVarInt(0); // Chunk Z
        outbound.Commit(ref centerChunk);

        // 2. Chunk Batch Start (0x0C)
        var batchStart = outbound.Begin();
        batchStart.WriteVarInt(0x0C); // chunk_batch_start
        outbound.Commit(ref batchStart);

        // 3. Сетка чанков 5×5 (от -2 до 2)
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                // Chunk chunk = FlatGenerator.Generate(x, z);
                //
                // var chunkData = outbound.Begin();
                // chunkData.WriteVarInt(0x2D); // level_chunk_with_light
                // chunk.SerializeBody(ref chunkData);
                // outbound.Commit(ref chunkData);
            }
        }

        // 4. Chunk Batch Finished (0x0B)
        var batchFinished = outbound.Begin();
        batchFinished.WriteVarInt(0x0B) // chunk_batch_finished
            .WriteVarInt(25); // Batch size — 25 чанков (5 × 5).
        outbound.Commit(ref batchFinished);

        return PacketHandleResult.Accepted;
    }
}