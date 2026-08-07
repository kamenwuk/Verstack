using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Compression;
using Verstack.Engine.Lifecycle;
using Verstack.Engine.Bridge;
using Verstack.Shared.Debug;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Shared;

/// <summary>
/// Сетевой health-check фазы Play: периодически шлёт <c>keep_alive</c> (clientbound 0x2C) каждому
/// подключённому игроку и кикает не ответивших за <c>KEEPALIVE_TIMEOUT</c>.
///
/// <para>Это проактивная отправка по таймеру — поэтому система, а не бандл (бандлы реактивны).
/// Шаблон аккумулятора — из <c>UpdateServerInfoSystem</c>, отправка — через <see cref="OutboundLease"/>
/// (как в доках <c>OutboundLease</c> для broadcast'ов). Ответ клиента разбирает
/// <see cref="KeepAliveResponseBundle"/> в <c>InboundDispatcherSystem</c>.</para>
///
/// <para><b>Отправка</b> — глобальный аккумулятор: раз в <c>KEEPALIVE_INTERVAL</c> помечаем тик как
/// «время слать» и шлём каждому <b>свободному</b> игроку (у кого <see cref="KeepAliveInf.IsAwaiting"/>=false).
/// Ждущему новый payload не шлём — иначе его <see cref="KeepAliveInf.Payload"/> перетёрся бы и ответ на
/// прошлый раунд дал бы ложный mismatch. <b>Таймаут</b> же проверяем каждый тик (а не только в тик
/// отправки), чтобы кик сработал вовремя, а не ждал до следующего интервала.</para>
///
/// <para>Регистрируется в <c>RealmLayer.Init</c> ПОСЛЕ <c>InboundDispatcherSystem</c>: ответ 0x1C,
/// пришедший в этом тике, должен успеть снять <see cref="KeepAliveInf.IsAwaiting"/> до проверки
/// таймаута здесь — иначе ложный кик на грани timeout'а.</para>
/// </summary>
internal sealed class KeepAliveSystem : IProtoInitSystem, IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeStateCacheStore = null!;
    [DI] private readonly UserSessionCacheStore _userSessionCacheStore = null!;
    [DI(ServerWorldScopes.GLOBAL)] private readonly IPacketCompressor _compressor = null!;
    [DI] private readonly ServerTime _serverTime = null!;

    private double _timer;
    private long _nextPayload;

    public void Init(IProtoSystems systems)
    {
        _timer = 0;
        _nextPayload = 0;
    }

    public void Run()
    {
        // Глобальный аккумулятор отправки. Таймаут ниже проверяем каждый тик независимо от него.
        _timer += _serverTime.DeltaTime;
        var timeToSend = false;
        if (_timer >= ServerConstants.KEEPALIVE_INTERVAL)
        {
            _timer = 0;
            timeToSend = true;
        }

        foreach (var entity in _userSessionCacheStore.ItPlaying)
        {
            // Защита: ItPlaying не требует KeepAliveInf, сеётся он в HandoffSeeder при завершении Join.
            if (!_userSessionCacheStore.KeepAlives.Has(entity))
                continue;

            ref var keepAlive = ref _userSessionCacheStore.KeepAlives.Get(entity);

            // Уже ждём ответ — повторно не шлём, payload должен остаться актуальным для сверки.
            if (keepAlive.IsAwaiting)
            {
                if (_serverTime.TotalTime - keepAlive.SentAt >= ServerConstants.KEEPALIVE_TIMEOUT)
                {
                    _bridgeStateCacheStore.GetChannel(entity).Disconnect();
                    Logger.Warn(LogKey.PacketPlayKeepAliveTimeout, (int)entity, ServerConstants.KEEPALIVE_TIMEOUT);
                }
                continue;
            }

            if (!timeToSend)
                continue;

            // Свободен и пришёл тик отправки — формируем новый keep_alive.
            // ++ даёт 1,2,3… — avoids 0 (значение seeded-дефолта).
            var payload = ++_nextPayload;
            keepAlive.Payload = payload;
            keepAlive.SentAt = _serverTime.TotalTime;
            keepAlive.IsAwaiting = true;

            var channel = _bridgeStateCacheStore.GetChannel(entity);
            using var outbound = OutboundLease.Acquire(channel, _compressor);
            var writer = outbound.Begin();
            writer.WriteVarInt(0x2C).WriteLong(payload);
            outbound.Commit(ref writer);
            // using → Dispose → Flush → zero-copy в очередь канала.

            Logger.Debug(LogKey.PacketPlayKeepAliveSent, (int)entity, payload);
        }
    }
}