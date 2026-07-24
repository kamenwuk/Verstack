# Фрейминг пакетов

Фрейминг — это то, что разбивает сырой поток байт TCP на отдельные пакеты Minecraft. Этот документ описывает схему фрейминга, применяемую в Minecraft, и реализующий её `PacketFrameScanner`.

## Проблема: TCP — это поток, а не последовательность сообщений

TCP доставляет упорядоченный, надёжный **поток байт** — у него нет понятия границ сообщений. Когда вы вызываете `ReadAsync` у `PipeReader`, вернувшийся чанк может быть чем угодно:

```
ReadAsync вернул 100 байт. Что внутри?
├─ может быть 3 целых пакета         [len][payload][len][payload][len][payload]
├─ может быть 1 целый + половина 2-го [len][payload][len][paylo...
├─ может быть только половина одного  [len][paylo...
└─ может быть конец одного + начало следующего ...oad][len][paylo...
```

Без правила «где заканчивается один пакет и начинается следующий» байты лишены смысла. Это правило — **фрейминг**.

## Фрейминг Minecraft: VarInt-length-prefix

Каждый пакет на проводе устроен так:

```
[ VarInt: длина payload ][ payload: N байт ]
         ↑ до 5 байт            ↑ ровно N байт
```

Чтобы извлечь один кадр:

1. Прочитать VarInt → узнаём `length` (размер payload в байтах).
2. Проверить, есть ли после него хотя бы `length` байт.
   - **есть** → целый кадр. Берём эти `length` байт как payload.
   - **нет** → данных пока мало. Ждём, пока буфер наполнится.
3. Сам payload начинается с VarInt packet ID, за которым идут поля пакета — но это уже парсинг пакета, не фрейминг. Фрейминг останавливается на извлечении payload длиной `length`.

## Кадр на проводе

Минимальный пример — payload из 3 байт `[0x10, 0x00, 0xFF]`:

```
VarInt(3) = 0x03        ← длина payload, один байт
payload   = 10 00 FF    ← 3 байта

байты на проводе: [ 03 10 00 FF ]
```

Двухбайтовая длина (payload из 300 байт) выражается мультибайтовым VarInt:

```
VarInt(300) = AC 02     ← длина payload, два байта
payload     = ...300 байт...

байты на проводе: [ AC 02 <300 байт> ]
```

См. [VarInt](varint.md) — как `300` становится `[AC 02]`.

## Частичные данные — это норма

В потоковом I/O нехватка байт посреди кадра — **ожидаемая** ситуация, а не исключительная. Кадр может приходить разрезанным на несколько вызовов `ReadAsync`. Есть два способа, которыми кадр может оказаться неполным:

| Ситуация | Что произошло |
|----------|---------------|
| VarInt не закрыт | Префикс длины сам разрезан: continuation-биты выставлены, но закрывающий байт ещё не пришёл. |
| Payload не полный | VarInt прочитан полностью (мы знаем `length`), но в буфере меньше `length` байт payload. |

В обоих случаях ответ один: **остановиться, запомнить, с чего начался неполный кадр, дождаться ещё данных, повторить.**

## Битые данные

Два режима отказа означают, что данные действительно сломаны (или кто-то шлёт мусор):

| Ситуация | Что произошло |
|----------|---------------|
| Переполнение VarInt | Continuation не закрывается за `MAX_SIZE` (5) байт — невозможно для валидного `int32`. |
| Слишком большая длина | `length` превышает настроенный `MaxPacketSize` (по умолчанию ~2 МБ) — атака на исчерпание памяти. |

В обоих случаях соединение нужно разорвать: на повреждённом потоке синхронизировать фрейминг невозможно.

## Чтение через границы сегментов

`PipeReader.ReadAsync` возвращает `ReadOnlySequence<byte>` — логический буфер, который может опираться на **несколько** несмежных сегментов памяти (это происходит, когда внутренний ring-буфер pipe заворачивается). Один кадр может быть разрезан границей сегментов в любой из частей:

```
сегмент A: [ ... 0xAC ]      ← первый байт VarInt(300)
сегмент B: [ 0x02 <payload> ] ← второй байт VarInt + payload
```

`PacketFrameScanner` использует внутри `SequenceReader<byte>` (из `System.Buffers`), который прозрачно проходит по границам сегментов. VarInt и payload считываются корректно даже в разрезанном виде — без копирования, без аллокаций.

## PacketFrameScanner

`PacketFrameScanner` — это `ref struct`-enumerator, реализующий описанную выше схему фрейминга.

### Решения по дизайну

- **`ref struct`** — только стек, ноль аллокаций, не может боксироваться. Соответствует конвенции DOD / GC-free проекта.
- **Одноразовый на `ReadAsync`** — привязан к одному `ReadOnlySequence<byte>`. Буфер невалиден после `AdvanceTo`, поэтому на каждое чтение создаётся свежий scanner.
- **Status-enum, а не исключения** — частичные чтения нормальны; бросать исключение на них означало бы аллокацию в горячем пути. `VarInt.ReadStatus` различает исходы.

### API

```csharp
public ref struct PacketFrameScanner
{
    // Стандартный лимит размера кадра Minecraft (~2 МБ).
    public const int DEFAULT_MAX_PACKET_SIZE = 2 * 1024 * 1024;

    public PacketFrameScanner(ReadOnlySequence<byte> input, int maxPacketSize = DEFAULT_MAX_PACKET_SIZE);

    // Продвинуться к следующему целому кадру.
    // true  → кадр доступен в Current.
    // false → причину смотреть в Status.
    public bool MoveNext();

    // Payload текущего кадра (валиден только после MoveNext()==true).
    public ReadOnlySequence<byte> Current { get; }

    // Причина последнего MoveNext()==false:
    //   Complete  → все кадры потреблены, конец буферизованных данных.
    //   Partial   → неполный кадр, нужно больше данных.
    //   Malformed → битый кадр, рвать соединение.
    public VarInt.ReadStatus Status { get; }

    // Позиция для PipeReader.AdvanceTo(consumed, examined).
    // При Partial указывает на НАЧАЛО неполного кадра,
    // чтобы его байты остались в буфере до следующего чтения.
    public SequencePosition ConsumedPosition { get; }

    // Поддержка foreach по целым кадрам.
    public PacketFrameScanner GetEnumerator() => this;
}
```

### Использование в цикле чтения

```csharp
ReadResult result = await reader.ReadAsync(token);
var scanner = new PacketFrameScanner(result.Buffer);

while (scanner.MoveNext())
{
    ReadOnlySequence<byte> payload = scanner.Current;
    // диспетчер: payload (например, парсинг packet ID + полей)
}

// Двухаргументный AdvanceTo: consumed = где остановился scanner,
// examined = конец буфера (сигнал «я просмотрел всё»).
reader.AdvanceTo(scanner.ConsumedPosition, result.Buffer.End);

switch (scanner.Status)
{
    case VarInt.ReadStatus.Partial:
        // неполный кадр остался в буфере; крутим ReadAsync ещё раз
        break;
    case VarInt.ReadStatus.Malformed:
        // рвём соединение
        break;
}
```

См. `src/Verstack.Protocol/PacketFrameScanner.cs`.
