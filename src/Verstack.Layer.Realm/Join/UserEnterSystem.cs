using System.Buffers;
using System.Buffers.Binary;
using Leopotam.EcsProto;
using Leopotam.EcsProto.QoL;
using Verstack.Debug;
using Verstack.Layer.Global.User;
using Verstack.Layer.Realm.User;
using Verstack.Lifecycle;
using Verstack.Network;
using Verstack.Network.Compression;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Realm.Systems;

internal sealed class UserEnterSystem : IProtoRunSystem
{
    // [DI] private readonly ZLibPacketCompressor _compressor = null!;
    // [DI(ServerWorldScopes.REALM)] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    //
    // private const int FRAME_SCRATCH_SIZE = 8192;
    // private const int PAYLOAD_BUFFER_SIZE = 16384;

    public void Run()
    {
//         byte[] frameArray = ArrayPool<byte>.Shared.Rent(FRAME_SCRATCH_SIZE);
//         byte[] payloadArray = ArrayPool<byte>.Shared.Rent(PAYLOAD_BUFFER_SIZE);
//         try
//         {
//             Span<byte> frameScratch = frameArray.AsSpan(0, FRAME_SCRATCH_SIZE);
//             Span<byte> payloadBuffer = payloadArray.AsSpan(0, PAYLOAD_BUFFER_SIZE);
//
//             // Подготовка 24 пустых секций и 24 пустых биомов (каждая по 5 байт)
//             Span<byte> sectionsData = stackalloc byte[256];
//             Span<byte> biomesData = stackalloc byte[256];
//             int secPos = 0, bioPos = 0;
//             for (int i = 0; i < 24; i++)
//             {
//                 // Секция
//                 BinaryPrimitives.WriteInt16BigEndian(sectionsData.Slice(secPos, 2), 0);
//                 secPos += 2;
//                 sectionsData[secPos++] = 0; // bitsPerBlock
//                 sectionsData[secPos++] = 0; // palette length (VarInt 0)
//                 sectionsData[secPos++] = 0; // data array length (VarInt 0)
//
//                 // Биом
//                 BinaryPrimitives.WriteInt16BigEndian(biomesData.Slice(bioPos, 2), 0);
//                 bioPos += 2;
//                 biomesData[bioPos++] = 0; // bitsPerBlock
//                 biomesData[bioPos++] = 0; // palette length (VarInt 0)
//                 biomesData[bioPos++] = 0; // data array length (VarInt 0)
//             }
//             int sectionSize = secPos; // 120
//             int biomeSize = bioPos;   // 120
//
//             foreach (var entity in _userSessionCacheStore.EnterPending)
//             {
//                 var channel = _userSessionCacheStore.GetChannel((int)entity);
//                 if (channel == null) continue;
//
//                 var userProfile = _userSessionCacheStore.UserProfiles.Get(entity);
//                 var outbound = new PacketOutbound(channel, _compressor, frameScratch, payloadBuffer);
//
//                 // === 1. Login (play) (ID 0x31) ===
//                 Logger.Debug(LogKey.PacketPlayLogin, userProfile.Username);
//                 var pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x31);
//                 int entityId = (int)entity + 1;
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), entityId); pw.Advance(4);
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Is Hardcore
//                 VarInt.Write(ref pw, 1);
//                 Utf8String.Write(ref pw, "minecraft:overworld"u8); // Dimension Names
//                 VarInt.Write(ref pw, 20); // Max Players
//                 VarInt.Write(ref pw, 10); // View Distance
//                 VarInt.Write(ref pw, 10); // Simulation Distance
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Reduced Debug Info
//                 pw.GetSpan(1)[0] = 1; pw.Advance(1); // Enable Respawn Screen
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Do Limited Crafting
//                 VarInt.Write(ref pw, 0); // Dimension Type
//                 Utf8String.Write(ref pw, "minecraft:overworld"u8); // Dimension
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8); // Seed
//                 pw.GetSpan(1)[0] = 1; pw.Advance(1); // Game Mode (Creative)
//                 pw.GetSpan(1)[0] = 0xFF; pw.Advance(1); // Previous Game Mode
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Is Debug
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Is Flat
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Has Death Location
//                 VarInt.Write(ref pw, 0); // Portal Cooldown
//                 VarInt.Write(ref pw, 63); // Sea Level
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Online Mode
//                 pw.GetSpan(1)[0] = 0; pw.Advance(1); // Enforces Secure Chat
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // === 2. Initialize World Border (ID 0x2B) ===
//                 Logger.Debug(LogKey.PacketPlayWorldBorder, userProfile.Username);
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x2B);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), BitConverter.DoubleToInt64Bits(59999968.0)); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), BitConverter.DoubleToInt64Bits(59999968.0)); pw.Advance(8);
//                 VarInt.Write(ref pw, 0);
//                 VarInt.Write(ref pw, 29999984);
//                 VarInt.Write(ref pw, 5);
//                 VarInt.Write(ref pw, 15);
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // === 3. Player Abilities (ID 0x40) ===
//                 Logger.Debug(LogKey.PacketPlayAbilities, userProfile.Username);
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x40);
//                 pw.GetSpan(1)[0] = 0x08 | 0x04; pw.Advance(1);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), BitConverter.SingleToInt32Bits(0.05f)); pw.Advance(4);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), BitConverter.SingleToInt32Bits(0.1f)); pw.Advance(4);
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // === 4. Player Info Update (ID 0x46) ===
//                 Logger.Debug(LogKey.PacketPlayInfoUpdate, userProfile.Username);
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x46);
//                 VarInt.Write(ref pw, 0x01 | 0x04 | 0x08 | 0x10);
//                 VarInt.Write(ref pw, 1);
//                 userProfile.Uuid.TryWriteBytes(pw.GetSpan(16), bigEndian: true, out _); pw.Advance(16);
//                 Utf8String.Write(ref pw, userProfile.Username);
//                 VarInt.Write(ref pw, 0); // properties
//                 VarInt.Write(ref pw, 1); // game mode
//                 pw.GetSpan(1)[0] = 1; pw.Advance(1); // listed
//                 VarInt.Write(ref pw, 0); // latency
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // === 5. Synchronize Player Position (ID 0x48) ===
//                 Logger.Debug(LogKey.PacketPlayPosition, userProfile.Username);
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x48);
//                 VarInt.Write(ref pw, 0);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), BitConverter.DoubleToInt64Bits(100.0)); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), 0); pw.Advance(4);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), 0); pw.Advance(4);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), 0); pw.Advance(4); // Relatives
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // // === 6. Set Default Spawn Position (ID 0x61) ===
//                 // Logger.Debug(LogKey.PacketPlaySpawnPosition, userProfile.Username);
//                 // pw = new SpanWriter(payloadBuffer);
//                 // VarInt.Write(ref pw, 0x61);
//                 // long spawnPos = (0L << 38) | (0L << 12) | (100L & 0xFFF);
//                 // BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), spawnPos); pw.Advance(8);
//                 // BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), 0); pw.Advance(4);
//                 // outbound.Send(pw.WrittenSpan);
//                 // if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
// // === 7. Set Render Distance (ID 0x5F) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x5F);
//                 VarInt.Write(ref pw, 10);
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
// // === 7b. Set Simulation Distance (ID 0x6F) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x6F);
//                 VarInt.Write(ref pw, 10);    // симуляционная дистанция (обычно равна видовой)
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 // === 8. Set Chunk Cache Center (ID 0x5E) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x5E);
//                 VarInt.Write(ref pw, 0); // chunk X
//                 VarInt.Write(ref pw, 0); // chunk Z
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
// // === 9a. Chunk Batch Start (ID 0x0C) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x0C);
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
// // === 9b. Отправка 9 пустых чанков (X: -1..1, Z: -1..1) ===
//                 for (int cx = -1; cx <= 1; cx++)
//                 {
//                     for (int cz = -1; cz <= 1; cz++)
//                     {
//                         pw = new SpanWriter(payloadBuffer);
//                         VarInt.Write(ref pw, 0x2D);
//                         BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), cx); pw.Advance(4);
//                         BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), cz); pw.Advance(4);
//
//                         // Heightmaps: 0
//                         VarInt.Write(ref pw, 0);
//
//                         // Data (секции + биомы)
//                         VarInt.Write(ref pw, sectionSize + biomeSize);
//                         sectionsData.Slice(0, sectionSize).CopyTo(pw.GetSpan(sectionSize));
//                         pw.Advance(sectionSize);
//                         biomesData.Slice(0, biomeSize).CopyTo(pw.GetSpan(biomeSize));
//                         pw.Advance(biomeSize);
//
//                         // Block Entities: 0
//                         VarInt.Write(ref pw, 0);
//
//                         // Light Data: всё пусто
//                         VarInt.Write(ref pw, 0); // skyYMask
//                         VarInt.Write(ref pw, 0); // blockYMask
//                         VarInt.Write(ref pw, 0); // emptySkyYMask
//                         VarInt.Write(ref pw, 0); // emptyBlockYMask
//                         VarInt.Write(ref pw, 0); // skyUpdates
//                         VarInt.Write(ref pw, 0); // blockUpdates
//
//                         outbound.Send(pw.WrittenSpan);
//                         if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//                     }
//                 }
//
// // === 9c. Chunk Batch Finished (ID 0x0B) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x0B);
//                 VarInt.Write(ref pw, 9); // 9 чанков в батче
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
//                 
//                 // === 10. Update Time (ID 0x71) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x71);
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8); // World Age
//                 BinaryPrimitives.WriteInt64BigEndian(pw.GetSpan(8), 0); pw.Advance(8); // Time of Day
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//
// // === 11. Set Experience (ID 0x67) ===
//                 pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x67);
//                 BinaryPrimitives.WriteInt32BigEndian(pw.GetSpan(4), BitConverter.SingleToInt32Bits(0f)); pw.Advance(4); // Experience bar
//                 VarInt.Write(ref pw, 0); // Level
//                 VarInt.Write(ref pw, 0); // Total XP
//                 outbound.Send(pw.WrittenSpan);
//                 if (outbound.Written > 0) { channel.EnqueueOutbound(outbound.WrittenSpan); outbound.Reset(); }
//                 
//                 _userSessionCacheStore.EnterPending.Del(entity);
//                 
//             }
//         }
//         finally
//         {
//             ArrayPool<byte>.Shared.Return(frameArray);
//             ArrayPool<byte>.Shared.Return(payloadArray);
//         }
    }
}