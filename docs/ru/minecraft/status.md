# Status

Состояние Status отвечает на пинги списка серверов. Клиент подключается, шлёт Handshake (с `next state = Status`), затем Status Request; сервер отвечает Status Response — JSON-payload с описанием сервера. Текущая реализация достигает этого результата сознательным сокращением — см. [Текущее состояние](#текущее-состояние) ниже.

## Три актёра

| Актёр        | Тип                                                                 |
|--------------|---------------------------------------------------------------------|
| DTO          | `ServerStatusResponse` — версия, ёмкость, MOTD                      |
| Сериализатор | `ServerStatusSerializer` — DTO → payload Status Response            |
| Handler      | `ServerStatusHandler` — `IPacketHandler` для состояния Status       |

DTO — это `readonly struct`: инертные поля, ноль зависимостей. Вложенные DTO держат части: `ServerVersion` (имя + номер протокола), `ServerCapacity` (max слотов + online).

Сериализатор — `static class`, кодирующий DTO в payload пакета — `[VarInt(packetId)][VarInt(jsonLen)][UTF-8 JSON]` — и пишущий только payload; фрейминг — задача `PacketFraming`. Handler — реализация `IPacketHandler`, реагирующая на входящие payload'ы и пишущая ответы; он композирует два других:

```
ServerStatusSerializer.Write(scratch, in status)   → байты payload
PacketFraming.Write(writer, scratch.WrittenSpan)   → кадр в PipeWriter
SessionLifetime: await writer.FlushAsync(token)    → в сокет
```

Сериализация сначала во временный буфер необходима, потому что `PacketFraming` требует payload как contiguous span, а сериализатор пишет его прямо в буфер — поэтому payload сначала складывается во временный `ArrayBufferWriter<byte>`, а затем оборачивается в кадр и пишется в соединение. Одна аллокация на исходящий пакет; в будущем — пул через `ArrayPool`.

## Формат на проводе

```
Status Request (клиент → сервер):  [0x00]                           ← packet id 0x00, пустой payload
Status Response (сервер → клиент): [0x00][VarInt(jsonLen)][JSON]    ← packet id 0x00, JSON-тело
```

JSON-тело:

```json
{
  "version":     { "name": "1.21.6", "protocol": 774 },
  "players":     { "max": 20, "online": 0 },
  "description": { "text": "A Minecraft Server" }
}
```

## Стиль сериализации

Сериализатор пишет в `IBufferWriter<byte>` (временный буфер), никогда не возвращает `byte[]` — это совпадает с `PacketFraming.Write` и избавляет от копирования. Сериализация двухфазная: тело JSON пишется во временный буфер через `Utf8JsonWriter` (длина неизвестна, пока тело не записано), затем итоговый payload пишется в выходной буфер одним contiguous-куском. Одна аллокация на холодном пути (пинги статуса редки); сериализация пакетов на горячем пути избежит временного буфера проходом «замер-затем-запись».

## Текущее состояние

`ServerStatusHandler` — заглушка: он отвечает сконфигурированным статусом на **любой** входящий кадр, без парсинга Handshake, отслеживания состояния протокола или различения packet id. Этого достаточно для первого видимого результата — MOTD в списке серверов — и откладывает настоящую механику на позже: парсер Handshake, переключающий соединение в Status или Login, state-машину, выбирающую активный handler, и диспетчеризацию по packet id (Status Request → Status Response, Ping → Pong).

→ [Слой Minecraft](index.md)
