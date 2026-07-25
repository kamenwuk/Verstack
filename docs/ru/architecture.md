# Архитектура

Карта кодовой базы Verstack: какие проекты есть, чем владеет каждый и в какую сторону могут идти зависимости. Детали реализации каждого слоя — на отдельных страницах.

## Структура решения

```
Verstack.slnx                          ← XML-формат решения .NET 10
Directory.Build.props                  ← общие настройки всех проектов
src/
├── Verstack.Network/                  ← TCP/сокеты + цикл PipeReader. Зависит от Protocol.
├── Verstack.Protocol/                 ← VarInt, фрейминг, чтение полей. Чистая логика, 0 NuGet-зависимостей.
├── Verstack.Minecraft/                ← семантика пакетов Minecraft. Зависит от Protocol и Network.
│   ├── Handshake/                     ← фаза Handshake: DTO, парсер.
│   ├── Status/                        ← фаза Status: DTO, сериализатор.
│   └── Session/                       ← инфраструктура сессии: фаза, диспетчер, фабрика.
└── Verstack.App/                      ← Program.cs, точка входа. AssemblyName=Verstack
tests/
├── Verstack.Protocol.Tests/           ← xUnit, гоняет Protocol через Span/Sequence
├── Verstack.Minecraft.Tests/          ← xUnit, гоняет сериализацию Minecraft через IBufferWriter
└── Verstack.Network.Tests/            ← xUnit, гоняет read-loop SessionLifetime через пару Pipe (без сокета)
```

## Как идут зависимости

```
App  ──►  Network  ──►  Protocol  ──►  (только BCL)
 │          ▲
 │          │ Minecraft реализует IPacketHandler
 └────►  Minecraft  ──►  Protocol  ──►  (только BCL)
```

Зависимость линейная, не симметричная: Minecraft ссылается на Network, никогда наоборот. Это Dependency Inversion — Network владеет контрактами `IPacketHandler` («как мне передать разобранный кадр в вышележащий слой») и `IPacketHandlerFactory` («как мне добыть handler на каждое соединение»), а Minecraft даёт их реализации. Правило слоистости здесь — *знай меньше, а не больше*: Network по-прежнему ничего не знает о пакетах Minecraft.

Все стрелки идут вниз, в сторону Protocol / BCL. Protocol — фундамент, на котором все стоят, и ссылается он только на базовую библиотеку классов.

| Слой        | Может ссылаться на                                | НЕ может ссылаться на        |
|-------------|---------------------------------------------------|------------------------------|
| `App`       | Network, Minecraft, Protocol                      | — (корень композиции)        |
| `Network`   | Protocol, контракты `IPacketHandler`/`IPacketHandlerFactory` | специфику пакетов Minecraft  |
| `Minecraft` | Network (`IPacketHandler`/`IPacketHandlerFactory`), Protocol | — (верхний слой)             |
| `Protocol`  | только BCL (`System.Buffers`)                     | сокеты, Network, Minecraft   |

- **Protocol никогда не ссылается на Network или Minecraft.** Тестируется изолированно через `Span<byte>` / `ReadOnlySequence<byte>`, без сокета.
- **Network никогда не ссылается на Minecraft.** Единственный путь из Minecraft в мир Network — реализации `IPacketHandler` и `IPacketHandlerFactory`, которые App подсовывает Network'у.

## Слои

### Verstack.Network

TCP-сокеты и циклы `PipeReader`/`PipeWriter`, превращающие сырой поток байт в обрамлённые payload'ы Minecraft и обратно. Построен на `Pipelines.Sockets.Unofficial` (raw-сокеты + pipe, Marc Gravell). Владеет `TcpServer`, `SessionLifetime` и контрактами `IPacketHandler`/`IPacketHandlerFactory`.

→ [Network](network/index.md)

### Verstack.Protocol

Чистая логика, без NuGet-зависимостей. Всё здесь работает с `Span<byte>` и `ReadOnlySequence<byte>`. Предоставляет `VarInt` (целые LEB128), пару фрейминга — `PacketFrameScanner` читает кадры из sequence, `PacketFraming` пишет их в буфер, — и `PacketReader`, читающий поля одного пакета из payload целого кадра.

→ [Protocol](protocol/index.md)

### Verstack.Minecraft

Слой, где байты становятся Minecraft. DTO пакетов, их сериализаторы и парсеры, организованные по фазам протокола (`Handshake`, `Status`; дальше `Login` и `Play`), а также диспетчер, маршрутизирующий кадры по `(фаза, packet id)`. Ссылается на Network только ради реализации `IPacketHandler`/`IPacketHandlerFactory`.

→ [Minecraft](minecraft/index.md)

### Verstack.App

Точка входа (`Program.cs`) и корень композиции: конструирует данные статуса и `PacketDispatcherFactory` (Minecraft), передаёт её в `TcpServer` (Network) и крутит сервер до Ctrl+C.

## Текущий статус

- ✅ TCP-listener на 25565, принимает соединения.
- ✅ Читает и фреймит входящие пакеты (`PacketFrameScanner`).
- ✅ Пишет исходящие кадры (`PacketFraming`).
- ✅ Handshake state machine: разбирает Handshake, переключает фазу, диспетчеризует по packet id (Status Request → Status Response, Ping → Pong).
- ✅ Status Response: реальный Minecraft-клиент 1.21.6 при пинге списка серверов видит MOTD, версию и слоты игроков.
- ⬜ Login — следующая фаза протокола: аутентификация, шифрование, сжатие.
- ✅ Конкурентность — accept-цикл не блокируется сессией: каждое соединение обслуживается в фоновой задаче, при остановке сервер ждёт хвостовые через `Task.WhenAll`.
- ✅ Разрыв соединения на мусорный пакет — handler возвращает `PacketVerdict.Disconnect`, SessionLifetime рвёт соединение после flush'а.
