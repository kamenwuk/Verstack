namespace Verstack.Layer.Realm.User;

internal readonly struct KeepAliveTimer(double lastSentTime, long currentPayload)
{
    /// <summary>
    /// Время (в секундах от ServerTime.TotalTime) последней отправки пакета.
    /// </summary>
    public readonly double LastSentTime = lastSentTime;

    /// <summary>
    /// Payload (ID), который был отправлен клиенту в миллисекундах. 
    /// Клиент обязан прислать его обратно.
    /// </summary>
    public readonly long CurrentPayload = currentPayload;
}