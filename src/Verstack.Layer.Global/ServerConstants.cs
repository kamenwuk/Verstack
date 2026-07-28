namespace Verstack.Layer.Global;

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
}