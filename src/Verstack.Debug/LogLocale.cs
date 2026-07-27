namespace Verstack.Debug;

public static class LogLocale
{
    // Словарь по умолчанию (Русский)
    private static Dictionary<LogKey, string> _messages = new()
    {
        { LogKey.ServerStart, "Запуск Verstack Server на порту {0}..." },
        { LogKey.ServerStarted, "Сервер успешно запущен и готов к подключениям." },
        { LogKey.ServerStop, "Получен сигнал остановки. Завершаем работу сервера..." },
        { LogKey.ServerStopped, "Сервер полностью остановлен." },
        { LogKey.ServerTickFailed, "Необработанное исключение в тике сервера (работа продолжается)." },
        
        { LogKey.NetworkNewConnection, "Новое подключение: {0}" },
        { LogKey.NetworkChannelDisconnected, "Канал отключен: {0}" },
        { LogKey.NetworkAcceptFailed, "Ошибка в accept-цикле (работа продолжается)." },
        { LogKey.NetworkSendLoopStarted, "Send-воркер запущен для канала: {0}" },

        { LogKey.ComposerRealmSkipped, "Realm пропущен: нет аспектов, мир не создаётся." },

        { LogKey.GatewayNewChannel, "Новый канал передан в Gateway из NetworkService." },
        { LogKey.GatewayStatusState, "Канал перешел в состояние Status (Пинг сервера)." },
        { LogKey.GatewayLoginState, "Канал перешел в состояние Login (Игрок заходит)." },
        { LogKey.GatewayHandshakeRejected, "Handshake отклонён (мусор/недостаточно данных), канал: {0}" },
        { LogKey.GatewayStatusInvalidPacket, "Невалидный Status-пакет, канал отключён: {0}" },
        { LogKey.GatewayPacketRejected, "Пакет отклонён конвейером, канал отключён: entity {0}" },

        { LogKey.PacketStatusRequest, "Получен Status Request. Отправляем JSON (MOTD)." },
        { LogKey.PacketPingRequest, "Получен Ping Request. Отправляем Pong." }
    };

    /// <summary>
    /// Получить отформатированную строку по ключу.
    /// </summary>
    public static string Get(LogKey key, params object[] args)
    {
        if (_messages.TryGetValue(key, out var msg))
        {
            // Подставляем аргументы (например, IP-адрес в {0})
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }
        return $"[UNKNOWN_LOG_KEY: {key}]";
    }

    /// <summary>
    /// Сменить язык логов (просто передай другой словарь)
    /// </summary>
    public static void SetLanguage(Dictionary<LogKey, string> newLocale)
    {
        _messages = newLocale;
    }
}