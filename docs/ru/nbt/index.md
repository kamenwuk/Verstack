# Модуль NBT

NBT (Named Binary Tag) — бинарный формат Minecraft для структурированных данных: block entities, item'ы, метаданные чанков, Registry Data. `Verstack.NBT` — фундаментальный модуль уровня DataTypes из Network, только отдельным проектом: 0 NuGet, зависит только от BCL.

Сейчас реализован только writer (сервер отправляет NBT; reader добавляется в Play, когда понадобится читать NBT от клиента). DOM-модель (`NbtCompound`/`NbtList` узлы) тоже отложена — на листинг-пути Registry Data NBT-тела не пишутся, а полная DOM простаивает. Цель фундамента — покрыть Play: block entities, item'ы, чанки.

Где этот модуль в графе зависимостей — см. [Архитектуру](../architecture.md).

## Wire-формат

Big-endian. Тип тега — один байт:

| ID | Тип          | Полезная нагрузка                                                   |
|----|--------------|---------------------------------------------------------------------|
| 0  | TAG_End      | (нет) — маркер конца compound                                      |
| 1  | TAG_Byte     | 1 знаковый байт                                                     |
| 2  | TAG_Short    | 2 байта BE, знаковый                                                |
| 3  | TAG_Int      | 4 байта BE, знаковый                                                |
| 4  | TAG_Long     | 8 байт BE, знаковый                                                 |
| 5  | TAG_Float    | 4 байта BE, IEEE 754 single                                         |
| 6  | TAG_Double   | 8 байт BE, IEEE 754 double                                          |
| 7  | TAG_Byte_Array | `Int` (BE, длина) + N байт                                       |
| 8  | TAG_String   | `Short` (BE, длина, max 32767) + modified-UTF-8 байты               |
| 9  | TAG_List     | `Byte` (тип элементов) + `Int` (BE, кол-во) + N элементов           |
| 10 | TAG_Compound | именованные теги до TAG_End                                         |
| 11 | TAG_Int_Array | `Int` (BE, длина) + N×4 байта BE                                   |
| 12 | TAG_Long_Array | `Int` (BE, длина) + N×8 байт BE                                   |

Имя тега (для именованных) кодируется как `Short` BE длины + modified-UTF-8 байты — та же кодировка, что TAG_String.

Контекст записи определяет, что пишет writer у тега:

- **В Compound** — каждый тег именованный: `[type-байт][Short длина имени][modified-UTF-8 имя][payload]`.
- **В List** — каждый элемент безымянный и без type-байта (тип и количество уже в заголовке List): `[payload]`.

### Networked vs disk root

С Configuration/Play (1.20.2+) NBT передаётся по сети в **networked**-формате. Байт типа корневого compound (`0x0A`) пишется всегда, поле имени пропускается:

```
Disk:    [0x0A][Short=0 (пустое имя)][children…][0x00]
Network: [0x0A]                     [children…][0x00]
```

`NbtWriter` пишет networked-root по умолчанию; disk-формат (для тестов/свёрки) включается параметром `networked: false`.

## Modified UTF-8 — `ModifiedUtf8`

Строки и имена NBT используют **Java modified UTF-8**, а не обычный `Encoding.UTF8`. Битовая разбивка та же, что у UTF-8; отличия — в крайних кейсах:

- `\0` (U+0000) кодируется как `0xC0 0x80` (2 байта), а не одиночным `0x00` — чтобы NUL-байт не встречался в payload.
- Символы вне BMP (> U+FFFF) идут через UTF-16 суррогатную пару, и каждый суррогат записывается отдельным 3-байтным блоком — итого 6 байт на символ, а не 4.

`ModifiedUtf8` — `internal static class`: `GetByteCount(string)` считает число байт (не символов), `Write(string, Span<byte>)` пишет. ASCII-символы (доминирующий случай для имён NBT-тегов) обрабатываются быстрой веткой без накладных расходов; векторизация (AVX2/SSE2, как в ObsidianMC) отложена — скалярного пути достаточно. Деталь реализации `NbtWriter`, не часть публичного API.

## Writer — `NbtWriter`

GC-free writer прямо в `Span<byte>`. Stateful `ref struct`: помнит контекст вложенности через стек `NbtFrame` (выделенный caller'ом через `stackalloc`) и сам решает, писать ли имя и type-байт. Один конструктор под горячий путь:

```csharp
Span<NbtFrame> frames = stackalloc NbtFrame[8];
var w = new NbtWriter(payloadBuffer, frames, networked: true);
```

API симметрично разводится по контексту. В Compound — именованные перегрузки:

- `BeginRootCompound()` / `EndCompound()` — корень (открывается безымянно: networked без имени, disk с пустым).
- `BeginCompound(name)` / `EndCompound()` — вложенный compound с именем.
- `BeginList(name, elementType, count)` / `EndList()` — список с именем.
- `WriteByte/Short/Int/Long/Float/Double/String/Bool(name, value)` — скаляры с именем.

В List — безымянные перегрузки (имя и type-байт не пишутся, счётчик элементов декрементируется):

- `BeginCompound()` / `BeginList(elementType, count)` — контейнеры-элементы.
- `WriteByte/Short/Int/Long/Float/Double/String/Bool(value)` — скаляры-элементы.

`EndCompound()` единый для root и вложенного: пишет `0x00` (TAG_End) и снимает кадр. `EndList()` ничего не пишет (длина уже в заголовке), только валидирует, что записано ровно заявленное число элементов.

`WrittenSpan` отдаёт готовый payload NBT. Все методы помечены `[MethodImpl(AggressiveInlining)]`.

### Почему `ref struct`, а не `sealed class`

`NbtWriter` держит `Span<byte>` поверх буфера caller'а (стекового или арендованного из `ArrayPool`). `Span<T>` — ref-структура и не может жить в heap-поле класса; значит, writer обязан быть `ref struct`. Цена — writer нельзя сохранить в поле ECS-компонента или передать через delegate/lambda, его жизнь ограничена стековым кадром. Это осознанная плата за GC-free: writer собирает NBT за один проход, без промежуточных аллокаций, и сразу флашится в канал.

## Кадры контекста — `NbtFrame`

`NbtFrame` — `public struct` (виден caller'у, чтобы передать `Span<NbtFrame>` в конструктор writer'а; для caller'а это opaque-буфер). Три поля: `Container` (Compound/List), `ExpectedListItem` (для List — тип элемента из заголовка), `ListRemaining` (сколько элементов ещё ждёт заголовок). Writer ведёт массив кадров как стек и мутирует верхний кадр через `ref`.

## Массивы — `NbtWriterArrayExtensions`

ByteArray/IntArray/LongArray вынесены в `internal static class` с методами расширения (`this ref NbtWriter`), чтобы ядро writer'а содержало только скалярный API. Массивы нужны chunk'ам и Registries (Play), для базового тестирования эталонными байтами необязательны — отсюда отдельный файл. Расширения видят `internal`-хелперы writer'а (`WriteNameAndType`, `OnListItem`, `WriteIntRaw`, `WriteSpan`) — тот же сборка, поэтому raw-методы подняты с `private` до `internal`.

## Валидация

Структурная валидация (контекст Compound/List, переполнение буфера и стека, рассогласование типов в List, длина строки ≤ 32767 байт) — только в `#if DEBUG`, через `[Conditional("DEBUG")]`-методы, которые JIT вырезает в Release. В горячем пути writer доверяет caller'у. Исключения — `InvalidOperationException` с префиксом `$"[{nameof(NbtWriter)}] ..."`.

## Текущие ограничения

- **Reader отложен до Play.** Writer тестируется сравнением с захардкоженными эталонными байтами (`Verstack.NBT.Tests`); reader добавляется, когда понадобится читать NBT от клиента.
- **DOM отложена.** Только прямой writer без дерева узлов.
- **Только `Span<byte>`.** Перегрузка под `IBufferWriter<byte>` (как у DataTypes из Network) добавится, когда NBT понадобится в горячем пути с фрагментированной записью.
- **Разблокирует Registry Data в Gateway.** Пакет Registry Data (S→C 0x07) требует NBT и пока не отправляется — см. [Gateway](../gateway/index.md). Writer готов, но листинг Registry Data идёт listing-only (тела опускаются), поэтому в горячем пути Configuration NBT пока не пишется.
