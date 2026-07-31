// using System.Buffers;
// using Leopotam.EcsProto;
// using Leopotam.EcsProto.QoL;
// using Verstack.Lifecycleasdf;
// using Verstack.Layer123.Realm.User;
// using Verstack.Network.Compression;
// using Verstack.Network.DataTypes;
// using Verstack.Network.Packet;
//
// namespace Verstack.Layer123.Realm;
//
// internal sealed class KeepAliveSystem : IProtoRunSystem
// {
//     [DI] private readonly RealmCacheStore _realmCacheStore = null!;
//     [DI] private readonly ZLibPacketCompressor _compressor = null!;
//     
//     // Инжектим твой ServerTime (убедись, что он зарегистрирован в DI)
//     [DI] private readonly ServerTime _serverTime = null!;
//     
//     private const int FRAME_SCRATCH_SIZE = 64;
//     private const int PAYLOAD_BUFFER_SIZE = 16;
//     private const double KEEP_ALIVE_INTERVAL_SEC = 10.0;
//     
//     public void Run()
//     {
//         byte[] frameArray = ArrayPool<byte>.Shared.Rent(FRAME_SCRATCH_SIZE);
//         byte[] payloadArray = ArrayPool<byte>.Shared.Rent(PAYLOAD_BUFFER_SIZE);
//         
//         try
//         {
//             Span<byte> frameScratch = frameArray.AsSpan(0, FRAME_SCRATCH_SIZE);
//             Span<byte> payloadBuffer = payloadArray.AsSpan(0, PAYLOAD_BUFFER_SIZE);
//
//             foreach (var entity in _realmCacheStore.Sessions)
//             {
//                 var channel = _realmCacheStore.GetChannel((int)entity);
//                 if (channel == null) continue;
//
//                 ref var timer = ref _realmCacheStore.KeepAliveTimers.Get(entity);
//                 
//                 // Проверяем, прошло ли 10 секунд с последней отправки
//                 if (_serverTime.TotalTime - timer.LastSentTime < KEEP_ALIVE_INTERVAL_SEC)
//                     continue;
//
//                 timer = new KeepAliveTimer(_serverTime.TotalTime, (long)(_serverTime.TotalTime * 1000.0));
//                 
//                 // Собираем пакет
//                 var pw = new SpanWriter(payloadBuffer);
//                 VarInt.Write(ref pw, 0x26); // ID: Keep Alive (clientbound)
//                 Numeric.WriteLong(ref pw, timer.CurrentPayload);
//
//                 // Отправляем в канал
//                 var outbound = new PacketOutbound(channel, _compressor, frameScratch, payloadBuffer);
//                 outbound.Send(pw.WrittenSpan);
//                 outbound.Flush();
//             }
//         }
//         finally
//         {
//             ArrayPool<byte>.Shared.Return(frameArray);
//             ArrayPool<byte>.Shared.Return(payloadArray);
//         }
//     }
// }