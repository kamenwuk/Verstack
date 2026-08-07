namespace Verstack.Engine.Lifecycle;

/// <summary>
/// Базовые неизменяемые константы сервера.
/// </summary>
public static class ServerConstants
{
    /// <summary>
    /// Целевое количество тиков в секунду (TPS). Стандарт для Minecraft — 20.
    /// </summary>
    public const int TICKS_PER_SECOND = 20;

    /// <summary>
    /// Длительность одного тика в секундах (1 / 20 = 0.05 сек = 50 мс).
    /// </summary>
    public const double TICK_INTERVAL = 1.0 / TICKS_PER_SECOND;
    
    /// <summary>
    /// Интервал обновления кэша сервера (в секундах).
    /// </summary>
    public const double SERVER_INFO_UPDATE_INTERVAL = 1.0;

    /// <summary>
    /// Порог сжатия пакетов (Set Compression, байты). Пакеты размером ≥ threshold сжимаются (zlib),
    /// меньшие уходят несжатыми в формате compressed-фрейминга (DataLength=0).
    ///
    /// Стандарт ванильного сервера — 256. Login-пакеты (~50 байт) уходят несжатыми (DataLength=0);
    /// сжиматься начнёт в Configuration/Play (Registry Data, чанки).
    /// -1 / отсутствие Set Compression — compression выключена (несжатый framing).
    /// </summary>
    public const int COMPRESSION_THRESHOLD = 256;

    /// <summary>
    /// Интервал отправки Keep Alive (clientbound 0x2C), секунды. Ванилла — 15с; здесь короче
    /// для отладки. Каждые INTERVAL сек сервер шлёт новый keep_alive всем свободным игрокам.
    /// </summary>
    public const double KEEPALIVE_INTERVAL = 5.0;

    /// <summary>
    /// Таймаут ответа на Keep Alive (serverbound 0x1C), секунды: не ответивший за TIMEOUT игрок
    /// дисконнектится. Должен быть ≥ <see cref="KEEPALIVE_INTERVAL"/>. Ванилла — 15с; здесь больше.
    /// </summary>
    public const double KEEPALIVE_TIMEOUT = 10.0;
}