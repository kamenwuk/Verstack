namespace Verstack.Layers.Realm;

/// <summary>
/// Per-entity состояние Keep Alive (сетевой health-check фазы Play): payload последнего
/// отправленного <c>keep_alive</c> (clientbound 0x2C), время его отправки и флаг ожидания ответа.
///
/// <para>Живёт всю сессию сущности (суффикс <c>...Inf</c> по конвенции). Сеётся в
/// <c>HandoffSeeder</c> при входе в мир в состоянии «свободен» (<see cref="IsAwaiting"/>=false),
/// так что <c>KeepAliveSystem</c> отправит первый запрос на ближайшем тике отправки.</para>
///
/// <para><b><see cref="IsAwaiting"/></b> блокирует повторную отправку: не ответивший вовремя игрок
/// не получает новый payload — поэтому его <see cref="Payload"/> остаётся актуальным для сверки,
/// а при истечении <c>KEEPALIVE_TIMEOUT</c> сущность дисконнектится.</para>
/// </summary>
public struct KeepAliveInf
{
    /// <summary>Payload последнего отправленного keep_alive (clientbound 0x2C). Клиент обязан вернуть тот же.</summary>
    public long Payload;

    /// <summary>Момент отправки последнего keep_alive, <c>ServerTime.TotalTime</c> (секунды). 0 — не отправляли.</summary>
    public double SentAt;

    /// <summary>true — отправили keep_alive, ждём ответ (0x1C). Новый не шлём, пока не ответит или не истечёт таймаут.</summary>
    public bool IsAwaiting;
}