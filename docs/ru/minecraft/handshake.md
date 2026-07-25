# Handshake

Фаза Handshake — точка входа в протокол: первый пакет, который клиент шлёт после подключения. Он несёт версию протокола, адрес, порт и фазу, в которую клиент хочет перейти. Сервер разбирает его и переключает фазу соединения; сам ответа не отправляет.

## Типы пакета

| Тип | Роль |
|---|---|
| `HandshakePacket` | DTO — версия протокола, адрес, порт, следующая фаза |
| `HandshakePacketParser` | Парсер — payload Handshake → DTO |

DTO — `readonly struct`: инертные поля. `HandshakeNextState` — отдельный enum (`Status = 1`, `Login = 2`), фиксирующий значения wire-протокола: что клиент *может запросить*, а не что сервер *обслуживает*. Это разграничение важно: сервер пока не реализует Login, но `HandshakeNextState.Login` всё равно валидное значение wire-формата, и парсер его принимает.

Парсер — `static class`, читающий тело пакета через `PacketReader` (после packet id, потреблённого диспетчером). Валидирует `nextState` на границе парсинга: значение вне `{1, 2}` — это кривой клиент, возвращается `false`.

## Формат на проводе

```
Handshake (клиент → сервер):  [0x00][VarInt(protoVersion)][VarInt(len)][UTF-8 адрес][ushort порт, BE][VarInt(nextState)]
                                ↑ packet id 0x00 в Handshake-фазе
```

Поля:

| Поле | Тип | Пример |
|---|---|---|
| protocolVersion | VarInt | `774` для 1.21.6 |
| serverAddress | length-prefixed UTF-8 | `localhost` |
| serverPort | ushort, big-endian | `25565` |
| nextState | VarInt | `1` = Status, `2` = Login |

`serverAddress` и `serverPort` информационные — это то, к чему клиент подключился (например, SRV-запись может указать на другой порт). Сервер пока их не использует, но разбирает и хранит в DTO для будущих фаз (Login их валидирует).

## Переключение фазы

Диспетчер получает разобранный `HandshakePacket` и по `NextState` переключает фазу соединения. Как именно — в [Диспетчере](dispatcher.md). Здесь зафиксирован только факт: `nextState = Status` переводит в фазу Status, `nextState = Login` пока не реализован и логируется, а фаза остаётся Handshake.

→ [Слой Minecraft](index.md)
