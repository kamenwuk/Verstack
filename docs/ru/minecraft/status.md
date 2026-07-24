# Status

Фаза Status отвечает на пинги списка серверов. Клиент подключается, шлёт Handshake (с `next state = Status`), затем Status Request; сервер отвечает Status Response — JSON-payload с описанием сервера. Дальше может прийти Ping, на который сервер отвечает Pong с тем же timestamp.

## Типы пакета

| Тип | Role |
|---|---|
| `ServerStatusResponse` | DTO — версия, ёмкость, MOTD |
| `ServerStatusSerializer` | Сериализатор — DTO → payload Status Response |

DTO — это `readonly struct`: инертные поля, ноль зависимостей. Вложенные DTO держат части: `ServerVersion` (имя + номер протокола), `ServerCapacity` (max слотов + online).

Сериализатор — `static class`, кодирующий DTO в payload пакета — `[VarInt(packetId)][VarInt(jsonLen)][UTF-8 JSON]` — и пишущий только payload; фрейминг — задача `PacketFraming`. Парсера для Status Response нет: сервер его отправляет, никогда не получает.

## Формат на проводе

```
Status Request (клиент → сервер):  [0x00]                           ← packet id 0x00, пустой payload
Status Response (сервер → клиент): [0x00][VarInt(jsonLen)][JSON]    ← packet id 0x00, JSON-тело
Ping (клиент → сервер):            [0x01][long timestamp, BE]       ← packet id 0x01
Pong (сервер → клиент):            [0x01][long timestamp, BE]       ← эхо того же timestamp
```

JSON-тело Status Response:

```json
{
  "version":     { "name": "1.21.6", "protocol": 774 },
  "players":     { "max": 20, "online": 0 },
  "description": { "text": "A Minecraft Server" }
}
```

Ping и Pong — это пара request/response фазы Status. Сервер читает timestamp из Ping и пишет его же обратно в Pong; клиент по разнице считает пинг для списка серверов. Диспетчеризация этих пакетов — в [Диспетчере](dispatcher.md).

## Стиль сериализации

Сериализатор пишет в `IBufferWriter<byte>` (временный буфер), никогда не возвращает `byte[]` — это совпадает с `PacketFraming.Write` и избавляет от копирования. Сериализация двухфазная: тело JSON пишется во временный буфер через `Utf8JsonWriter` (длина неизвестна, пока тело не записано), затем итоговый payload пишется в выходной буфер одним contiguous-куском. Одна аллокация на холодном пути (пинги статуса редки); сериализация пакетов на горячем пути избежит временного буфера проходом «замер-затем-запись».

→ [Слой Minecraft](index.md)
