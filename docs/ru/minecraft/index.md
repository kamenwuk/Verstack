# Слой Minecraft

Minecraft — слой, где байты становятся Minecraft. Здесь живут DTO пакетов, их сериализаторы и handler'ы, реагирующие на них. Слой организован по состояниям протокола, потому что wire-формат Minecraft зависит от текущего состояния: packet id `0x00` в Status, Login и Play значит разное, и их DTO не должны конфликтовать.

Сейчас есть только состояние Status (`Status/`); Login и Play последуют в том же устройстве, когда появятся.

Где этот слой в графе зависимостей — см. [Архитектуру](../architecture.md).

## Организация

```
Verstack.Minecraft/
└── Status/                    ← состояние Status
    ├── ServerStatusResponse.cs       ← DTO: версия, ёмкость, MOTD
    ├── ServerVersion.cs              ← вложенный DTO: имя версии + номер протокола
    ├── ServerCapacity.cs             ← вложенный DTO: max слотов + online
    ├── ServerStatusSerializer.cs     ← DTO → payload Status Response
    └── ServerStatusHandler.cs        ← IPacketHandler для состояния Status
```

## Три актёра

Каждый тип пакета выражается через три сотрудничающих типа, разделяя данные, кодирование и поведение так, чтобы каждый можно было тестировать изолированно.

**DTO** — это `readonly struct`: инертные поля, ноль зависимостей. **Сериализатор** — `static class`, кодирующий DTO в payload пакета (`VarInt` + поля); он пишет только payload, фрейминг — задача `PacketFraming`. **Handler** — реализация `IPacketHandler`, реагирующая на входящие payload'ы и пишущая ответы; единственный актёр, касающийся соединения.

Для состояния Status это `ServerStatusResponse`, `ServerStatusSerializer` и `ServerStatusHandler`. Handler композирует два других:

```
ServerStatusSerializer.Write(scratch, in status)   → байты payload
PacketFraming.Write(writer, scratch.WrittenSpan)   → кадр в PipeWriter
SessionLifetime: await writer.FlushAsync(token)    → в сокет
```

Сериализация сначала во временный буфер необходима, потому что `PacketFraming` требует payload как contiguous span, а сериализатор пишет его прямо в буфер — поэтому payload сначала складывается во временный `ArrayBufferWriter<byte>`, а затем оборачивается в кадр и пишется в соединение. Одна аллокация на исходящий пакет; в будущем — пул через `ArrayPool`.

## Стиль сериализации

Сериализаторы пишут в `IBufferWriter<byte>` (временный буфер или `PipeWriter` для уже обрамлённых данных), никогда не возвращают `byte[]`. Это совпадает с `PacketFraming.Write` и избавляет от копирования. Для JSON-payload'ов вроде Status Response сериализация двухфазная: тело JSON пишется во временный буфер через `Utf8JsonWriter` (длина неизвестна, пока тело не записано), затем итоговый payload — `[VarInt(packetId)][VarInt(jsonLen)][JSON]` — пишется в выходной буфер одним contiguous-куском. Это одна аллокация на холодном пути (пинги статуса редки); сериализация пакетов на горячем пути избежит временного буфера проходом «замер-затем-запись».

## Текущее состояние: handler-заглушка

`ServerStatusHandler` отвечает сконфигурированным статусом на **любой** входящий кадр, без парсинга Handshake, отслеживания состояния протокола или различения packet id. Этого достаточно для первого видимого результата — MOTD в списке серверов — и откладывает настоящую механику на позже.

Что заменит заглушку: парсер Handshake, читающий версию протокола, адрес сервера, порт и `next state` и переключающий соединение в Status или Login; state-машина, отслеживающая текущее состояние и выбирающая активный handler; и диспетчеризация по packet id внутри состояния (Status Request → Status Response, Ping → Pong).
