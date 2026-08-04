namespace Verstack.Shared.Debug;

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
        { LogKey.NetworkMalformedFrame, "Битый кадр (невалидная длина / мусорный zlib), канал отключён: {0}" },
        
        { LogKey.ComposerRealmSkipped, "Realm пропущен: нет аспектов, мир не создаётся." },

        { LogKey.GatewayNewChannel, "Новый канал передан в Gateway из NetworkService." },
        { LogKey.GatewayStatusState, "Канал перешел в состояние Status (Пинг сервера)." },
        { LogKey.GatewayLoginState, "Канал перешел в состояние Login (Игрок заходит)." },
        { LogKey.GatewayHandshakeRejected, "Handshake отклонён (мусор/недостаточно данных), канал: {0}" },
        { LogKey.GatewayStatusInvalidPacket, "Невалидный Status-пакет, канал отключён: {0}" },
        { LogKey.GatewayPacketRejected, "Пакет отклонён конвейером, канал отключён: entity {0}" },

        { LogKey.PacketStatusExchange, "Status Request -> Status Response (JSON/MOTD) записан в буфер." },
        { LogKey.PacketPingPong, "Ping Request -> Pong Response (эхо Timestamp) записан в буфер." },
        { LogKey.PacketLoginStart, "Login Start ({0}) -> Login Success записан в буфер (offline UUID сгенерирован)." },
        { LogKey.PacketLoginAcknowledged, "Login Acknowledged получен от {0} — фаза Login завершена." },

        { LogKey.PacketClientInformation, "Client Information (locale={0}) -> Clientbound Known Packs (0x0E) записан в буфер." },
        { LogKey.PacketKnownPacks, "Known Packs (0x07) получен -> Registry Data (0x07) отправлен клиенту (Шаг 0)." },
        
        { LogKey.PacketUpdateTags, "Шаг 1: Пакеты Update Tags (0x0D) обработаны и отправлены." },
        { LogKey.PacketConfigurationFinish, "Шаг 2: Feature Flags (0x0C) + Finish Configuration (0x03) отправлены." },
        
        { LogKey.PacketPlayDisconnect, "Acknowledge Finish Configuration (0x03) получен от {0}. Отправлен Disconnect (0x20) — Play фаза в разработке." },

        { LogKey.PacketRealmTransfer, "Сессия {0} передана в Realm. Ожидание инициализации Play." },
        { LogKey.PacketPlayLogin, "Realm -> {0}: Отправлен Login (Play) (0x31)" },
        { LogKey.PacketPlayWorldBorder, "Realm -> {0}: Отправлен Initialize World Border (0x2B)" },
        { LogKey.PacketPlayAbilities, "Realm -> {0}: Отправлен Player Abilities (0x40)" },
        { LogKey.PacketPlayInfoUpdate, "Realm -> {0}: Отправлен Player Info Update (0x46)" },
        { LogKey.PacketPlayPosition, "Realm -> {0}: Отправлен Synchronize Player Position (0x48)" },
        { LogKey.PacketPlaySpawnPosition, "Realm -> {0}: Отправлен Set Default Spawn Position (0x61)" },
        { LogKey.PacketPlayCommands, "Realm -> {0}: Отправлен Commands (0x10) (Пустой граф команд)" },
        { LogKey.PacketPlayTeleportConfirm, "Realm <- {0}: Получен Confirm Teleportation (0x00). ID: {1}" },
        { LogKey.PacketPlayMove, "Realm <- {0}: Получен Set Player Position (0x1A). XYZ: {1}, {2}, {3}" }
    };

    /// <summary>
    /// Получить отформатированную строку по ключу.
    /// </summary>
    public static string Get(LogKey key, params object[] args)
    {
        if (_messages.TryGetValue(key, out var msg))
        {
            // Добавлена проверка на null, так как Logger.Error передает null
            return args is { Length: > 0 } ? string.Format(msg, args) : msg;
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