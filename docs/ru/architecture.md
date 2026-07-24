# Архитектура

Карта кодовой базы Verstack: какие проекты есть, чем владеет каждый и в какую сторону могут идти зависимости. Детали реализации каждого слоя — на отдельных страницах.

## Структура решения

```
Verstack.slnx                          ← XML-формат решения .NET 10
Directory.Build.props                  ← общие настройки всех проектов
src/
├── Verstack.Network/                  ← TCP/сокеты + цикл PipeReader. Зависит от Protocol.
├── Verstack.Protocol/                 ← VarInt, фрейминг. Чистая логика, 0 NuGet-зависимостей.
├── Verstack.Minecraft/                ← семантика пакетов Minecraft. Зависит от Protocol и Network.
│   └── Status/                        ← Status-фаза: DTO, сериализатор, handler.
└── Verstack.App/                      ← Program.cs, точка входа. AssemblyName=Verstack
tests/
├── Verstack.Protocol.Tests/           ← xUnit, гоняет Protocol через Span/Sequence
└── Verstack.Minecraft.Tests/          ← xUnit, гоняет сериализацию Minecraft через IBufferWriter
```

## Как идут зависимости

```
App  ──►  Network  ──►  Protocol  ──►  (только BCL)
 │          ▲
 │          │ Minecraft реализует IPacketHandler
 └────►  Minecraft  ──►  Protocol  ──►  (только BCL)
```

Зависимость линейная, не симметричная: Minecraft ссылается на Network, никогда наоборот. Это Dependency Inversion — Network владеет контрактом `IPacketHandler` («как мне передать разобранный кадр в вышележащий слой»), а Minecraft даёт его реализацию. Правило слоистости здесь — *знай меньше, а не больше*: Network по-прежнему ничего не знает о пакетах Minecraft.

Все стрелки идут вниз, в сторону Protocol / BCL. Protocol — фундамент, на котором все стоят, и ссылается он только на базовую библиотеку классов.

| Слой        | Может ссылаться на                       | НЕ может ссылаться на        |
|-------------|------------------------------------------|------------------------------|
| `App`       | Network, Minecraft, Protocol             | — (корень композиции)        |
| `Network`   | Protocol, контракт `IPacketHandler`      | специфику пакетов Minecraft  |
| `Minecraft` | Network (`IPacketHandler`), Protocol     | — (верхний слой)             |
| `Protocol`  | только BCL (`System.Buffers`)            | сокеты, Network, Minecraft   |

- **Protocol никогда не ссылается на Network или Minecraft.** Тестируется изолированно через `Span<byte>` / `ReadOnlySequence<byte>`, без сокета.
- **Network никогда не ссылается на Minecraft.** Единственный путь из Minecraft в мир Network — реализация `IPacketHandler`, которую App подсовывает Network'у.

## Слои

### Verstack.Network

TCP-сокеты и циклы `PipeReader`/`PipeWriter`, превращающие сырой поток байт в обрамлённые payload'ы Minecraft и обратно. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + pipe, Marc Gravell). Владеет `TcpServer`, `SessionLifetime` и контрактом `IPacketHandler`.

→ [Network](network/index.md)

### Verstack.Protocol

Чистая логика, без NuGet-зависимостей. Всё здесь работает с `Span<byte>` и `ReadOnlySequence<byte>`. Предоставляет `VarInt` (целые LEB128) и пару фрейминга — `PacketFrameScanner` читает кадры из sequence, `PacketFraming` пишет их в буфер.

→ [VarInt](protocol/varint.md), [Фрейминг пакетов](protocol/packet-framing.md)

### Verstack.Minecraft

Слой, где байты становятся Minecraft. DTO пакетов, их сериализаторы и handler'ы, организованные по состояниям протокола (сейчас `Status`; дальше `Login` и `Play`). Ссылается на Network только ради реализации `IPacketHandler`.

→ [Minecraft](minecraft/index.md)

### Verstack.App

Точка входа (`Program.cs`) и корень композиции: конструирует данные статуса и `ServerStatusHandler` (Minecraft), передаёт его в `TcpServer` (Network) и крутит сервер до Ctrl+C.

## Текущий статус

- ✅ TCP-listener на 25565, принимает соединения.
- ✅ Читает и фреймит входящие пакеты (`PacketFrameScanner`).
- ✅ Пишет исходящие кадры (`PacketFraming`).
- ✅ Status Response: реальный Minecraft-клиент 1.21.6 при пинге списка серверов видит MOTD, версию и слоты игроков.
- ⬜ Handshake state machine — разобрать пакет Handshake, переключить состояние протокола, диспетчеризовать по packet id.
- ⬜ Настоящий диспетчер — `ServerStatusHandler` сейчас отвечает на *любой* кадр статусом (сознательная заглушка); нужно отвечать только на Status Request и отвечать на Ping пакетом Pong.
