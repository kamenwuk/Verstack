using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Shared.Voxel.Generation;
using Verstack.Layers.Realm.Chunks;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Shared.Voxel.Storage;

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
    // Stateless, возвращает идентичные колонки для любых координат — один экземпляр на сервер.
    private ChunkBufferPool _pool = null!;

    public override int StepCount => 1;
    
    public override void Init(IProtoSystems systems)
    {
        _pool = systems.GetService<ChunkBufferPool>();
    }

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        // 0. game_event (0x26), event 13: Start waiting for level chunks — прелюдия к batch.
        var gameEvent = outbound.Begin();
        gameEvent.WriteVarInt(0x26)
            .WriteByte(13) // Event — 13: Start waiting for level chunks.
            .WriteFloat(0.0f); // Value — для события 13 игнорируется, по стандарту 0.0f.
        outbound.Commit(ref gameEvent);

        // 1. Set Center Chunk (0x5E) — центр сетки загрузки (0,0).
        var centerChunk = outbound.Begin();
        centerChunk.WriteVarInt(0x5E)
            .WriteVarInt(0) // Chunk X
            .WriteVarInt(0); // Chunk Z
        outbound.Commit(ref centerChunk);

        // 2. Chunk Batch Start (0x0C)
        var batchStart = outbound.Begin();
        batchStart.WriteVarInt(0x0C); // chunk_batch_start
        outbound.Commit(ref batchStart);

        // 3. Сетка чанков 5×5 (от -2 до 2) — FlatGenerator, 25 колонок.
        var wire = new ChunkWireWriter();
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                var column = _pool.Acquire(x, z);

                var chunkData = outbound.Begin();
                chunkData.WriteVarInt(0x2D); // level_chunk_with_light
                wire.Write(ref chunkData, column, x, z);
                outbound.Commit(ref chunkData);
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