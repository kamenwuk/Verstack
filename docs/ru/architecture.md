# Архитектура

Справочник по структуре решения Verstack, слоям и зависимостям.

## Структура решения

```
Verstack.slnx                          ← XML-формат решения .NET 10
Directory.Build.props                  ← общие настройки всех проектов
src/
├── Verstack.Network/                  ← TCP/сокеты + цикл PipeReader. Зависит от Protocol.
├── Verstack.Protocol/                 ← VarInt, фрейминг. Чистая логика, 0 NuGet-зависимостей.
└── Verstack.App/                      ← Program.cs, точка входа. AssemblyName=Verstack
tests/
└── Verstack.Protocol.Tests/           ← xUnit, гоняет Protocol через Span/Sequence
```

## Слои и направление зависимостей

```
App  →  Network  →  Protocol  →  (только BCL)
```

| Слой       | Знает про                            | НЕ знает про                 |
|------------|--------------------------------------|------------------------------|
| `App`      | Network, Protocol                    | семантику Minecraft          |
| `Network`  | Protocol (`PacketFrameScanner`)      | семантику пакетов Minecraft  |
| `Protocol` | только BCL (`System.Buffers`)        | сокеты, Network, Minecraft   |

**Ключевое правило:** Protocol никогда не ссылается на Network. Protocol тестируется изолированно через
`Span<byte>` / `ReadOnlySequence<byte>`, без сокета.

## Verstack.Network

Зависит от `Pipelines.Sockets.Unofficial` (raw-сокеты + `System.IO.Pipelines`, Marc Gravell).

| Тип                  | Ответственность                                                      |
|----------------------|----------------------------------------------------------------------|
| `TcpServer`          | Слушающий сокет + accept-цикл. Создаёт `SocketConnection`, передаёт `SessionLifetime`. |
| `SessionLifetime`    | Жизнь одного соединения: цикл `PipeReader`, фрейминг через `PacketFrameScanner`, диспетчер кадров. |

### Цикл чтения (SessionLifetime.RunAsync)

```
loop:
    ReadResult = await reader.ReadAsync(token)
    scanner = new PacketFrameScanner(result.Buffer)
    while scanner.MoveNext(): dispatch(scanner.Current)  // один payload = один кадр Minecraft
    reader.AdvanceTo(scanner.ConsumedPosition, result.Buffer.End)
    if Malformed → рвём соединение
    if result.IsCompleted → break
reader.CompleteAsync()   // в finally
```

- `AdvanceTo(consumed, examined)` с двумя аргументами — корректный backpressure.
- При `Partial` `ConsumedPosition` указывает на начало недочитанного кадра, чтобы хвост остался в буфере.
- Один scanner на `ReadAsync` — `result.Buffer` невалиден после `AdvanceTo`.

## Verstack.Protocol

Чистая логика, 0 NuGet-зависимостей. Тестируется через `Span<byte>` / `ReadOnlySequence<byte>`.

| Тип                   | Ответственность                                                     |
|-----------------------|----------------------------------------------------------------------|
| `VarInt`              | LEB128 encode/decode `int`. `Encode`/`TryDecode` на `Span`, `TryRead` на `SequenceReader`. Вложенный enum `ReadStatus` (`Complete`/`Partial`/`Malformed`). |
| `PacketFrameScanner`  | `ref struct`-enumerator. Разбивает `ReadOnlySequence<byte>` на целые кадры Minecraft (VarInt-length-prefix). Одноразовый на `ReadAsync`. |

### Фрейминг

Каждый кадр на проводе:

```
[ VarInt: длина payload ][ payload: N байт ]
```

См. [Protocol/VarInt](protocol/varint.md) — кодирование LEB128.

## Verstack.App

Точка входа (`Program.cs`). Создаёт `TcpServer`, связывает Ctrl+C → `CancellationTokenSource`, запускает `server.RunAsync(token)`.

## Текущий статус

- ✅ TCP-listener на 25565, принимает соединения.
- ✅ Читает и фреймит входящие пакеты (`PacketFrameScanner`).
- ✅ Проверено end-to-end: handshake реального Minecraft-клиента (1.21.6) декодирован корректно.
- ⬜ Packet writer / исходящие пакеты (Status Response) — не реализовано.
- ⬜ Handshake state machine — не реализовано.
