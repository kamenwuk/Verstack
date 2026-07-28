# Модуль NBT

NBT (Named Binary Tag) — бинарный формат Minecraft для структурированных данных: block entities, item'ы, метаданные чанков, Registry Data. `Verstack.NBT` — фундаментальный модуль уровня DataTypes из Network, только отдельным проектом: 0 NuGet, зависит только от BCL.

Реализованы writer и reader — оба GC-free `ref struct`, симметричные по API. Writer пишет NBT от сервера (Registry Data, будущие block entities/items в Play); reader читает NBT из дампов и потоков (загрузка ванильного датапака для `Verstack.Vanilla`, в будущем — NBT от клиента). DOM-модель (`NbtCompound`/`NbtList` узлы) отложена — потоковый reader покрывает задачи без дерева узлов. Цель фундамента — покрыть Play: block entities, item'ы, чанки.

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

Контекст (Compound или List) определяет, как тег лежит в потоке — и writer, и reader согласованы с этим:

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

`ModifiedUtf8` — `internal static class`: `GetByteCount(string)` считает число байт (не символов), `Write(string, Span<byte>)` кодирует, `Read(ReadOnlySpan<byte>, Span<char>, out int charsWritten)` декодирует обратно (симметрия с `Write`, включая `\0` и суррогаты). Все три метода — zero-alloc: `Read` пишет в caller'ов `Span<char>`, не аллоцируя `string`. ASCII-символы (доминирующий случай для имён NBT-тегов) обрабатываются быстрой веткой без накладных расходов (widen byte→char); векторизация (AVX2/SSE2, как в ObsidianMC) отложена — скалярных путей достаточно. Буфер destination резервируется размером с `source.Length` (max возможный: char-счёт ≤ byte-счёт). Деталь реализации `NbtWriter`/`NbtReader`, не часть публичного API.

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

## Reader — `NbtReader`

GC-free reader из `ReadOnlySpan<byte>`, полный зеркало `NbtWriter`: те же поля (`_buffer`, `_frames`, `_networked`, `_offset`, `_depth`), тот же `NbtFrame`-стек. Один конструктор под горячий путь:

```csharp
Span<NbtFrame> frames = stackalloc NbtFrame[8];
var r = new NbtReader(sourceBuffer, frames, networked: true);
```

API — zero-alloc во всём: ни одна операция не аллоцирует. Два режима обхода, оба поверх одного ядра.

**Имена тегов — zero-copy byte-slice.** NBT-имена в реестрах Minecraft — все ASCII (`"type"`, `"value"`, `"minecraft:chat_type"`), для них modified-UTF-8 = ASCII byte-per-char. Поэтому `ReadTagName` возвращает имя как `ReadOnlySpan<byte>` — срез сырых байт прямо из буфера reader'а, без декодирования. Caller сравнивает с литералом через `SequenceEqual("count"u8)`. Для non-ASCII имён (`"café"`u8) — тот же byte-compare работает (mUTF-8 = UTF-8 для BMP без `\0`). `\0` в имени и суррогаты не покрываются — но таких имён в NBT не существует.

**Строковые значения — декодер в `Span<char>`.** Сами значения строк (ID, текстуры, локализации, emoji) могут быть любыми; для них нужен полный декодер mUTF-8 → UTF-16 char, но в caller'ов буфер: `ReadString(Span<char> destination, out int charsWritten)`. Буфер резервируется размером с источник (`stackalloc char[source.Length]` — max возможный: char-счёт ≤ byte-счёт).

**Sequental-core** — peek тега + чтение payload, симметрично writer'у. В Compound-контексте:

- `EnterRootCompound()` / `ExitCompound()` — корень. `ExitCompound` читает `0x00` (TAG_End) и снимает кадр (writer пишет симметрично).
- `EnterCompound()` — вход в вложенный compound (после peek, ничего не читает, только push кадр).
- `EnterList(out type, out count)` / `ExitList()` — list (count берётся из заголовка; `ExitList` только валидирует остаток, ничего не читает — симметрия с `EndList`).
- `ReadTagName(out type, out ReadOnlySpan<byte> utf8Name)` — peek: читает `[type+name]`, payload не трогает. Возвращает zero-copy срез имени. При `type == TAG_End` возвращает `End` и **откатывает offset** на 1 байт — End не потребляется, его прочитает `ExitCompound`.
- `ReadByte/Short/Int/Long/Float/Double/BoolPayload()` — чтение payload конкретного типа после peek.
- `ReadStringPayload(Span<char>, out int)` — то же для строк (через `ModifiedUtf8.Read`).

В List-контексте — безымянные перегрузки `ReadByte/Short/Int/Long/Float/Double/Bool()` (без peek, тип уже объявлен в заголовке List) и `ReadString(Span<char>, out int)`.

**Lookup** — поиск тега по имени внутри Compound, основной сценарий `Verstack.Vanilla` (чтение ванильных реестров):

- `TryReadByte/Short/Int/Long/Float/Double/Bool(ReadOnlySpan<byte> nameUtf8, out value)` — scan вперёд до имени, пропуская несовпадающие теги через `SkipPayload`. Сравнение имён — побайтовое (`SequenceEqual`), без декодирования: и caller'а, и в потоке — mUTF-8 байты. Возвращает `false`, если имя не найдено.
- `TryReadString(ReadOnlySpan<byte> nameUtf8, Span<char> destination, out int charsWritten)` — то же для строк, значение декодируется в destination.
- `TryEnterCompound(ReadOnlySpan<byte> nameUtf8)` / `TryEnterList(ReadOnlySpan<byte> nameUtf8, out type, out count)` — то же для контейнеров.
- `SkipRemaining()` — пропустить все оставшиеся теги Compound до (но не включая) TAG_End. Удобен после lookup'ов: нужные поля прочитаны, остальное не интересует — пропустили и закрыли.

Инвариант: lookup **только вперёд, без перемотки**. Если имя найдено — повторный lookup того же имени вернёт `false` (caller уже продвинулся мимо него). Если поле отсутствует в потоке (что бывает в эволюционирующих схемах) — это не ломает compound: false-lookup оставляет cursor на TAG_End, и можно либо продолжать lookup, либо выйти через `ExitCompound`. Один промах не закрывает compound — это критично для чтения ванильных датапаков разных версий.

### Почему `ref struct`, а не `sealed class` — общее для writer и reader

Те же причины, что у `NbtWriter`: `NbtReader` держит `ReadOnlySpan<byte>` (ref-структура) поверх буфера caller'а, значит сам обязан быть `ref struct`. Жизнь ограничена стековым кадром — нельзя сохранить в поле или передать в lambda. В тестах это требует 4-строчного boilerplate'а на каждый случай (нельзя замкнуть `r` в `Assert.Throws<T>(() => r.X())` — ручной try/catch).

## Кадры контекста — `NbtFrame`

`NbtFrame` — `public struct` (виден caller'у, чтобы передать `Span<NbtFrame>` в конструктор writer'а; для caller'а это opaque-буфер). Три поля: `Container` (Compound/List), `ExpectedListItem` (для List — тип элемента из заголовка), `ListRemaining` (сколько элементов ещё ждёт заголовок). Writer ведёт массив кадров как стек и мутирует верхний кадр через `ref`.

## Массивы — `NbtWriterArrayExtensions` / `NbtReaderArrayExtensions`

ByteArray/IntArray/LongArray вынесены в `internal static class` с методами расширения (`this ref NbtWriter` / `this ref NbtReader`), чтобы ядро содержало только скалярный API. Массивы нужны chunk'ам и Registries (Play), для базового тестирования эталонными байтами необязательны — отсюда отдельные файлы. Расширения видят `internal`-хелперы (`WriteNameAndType`, `OnListItem`, `ReadIntRaw`, `ReadSpan` и т.д.) — та же сборка, поэтому raw-методы подняты с `private` до `internal`.

Endianness-асимметрия у reader'а: ByteArray читается как zero-copy `ReadOnlySpan<byte>` (байт неделим, endian не важен, срез ссылается на буфер reader'а). IntArray/LongArray требуют BE→host-преобразования, поэтому caller даёт destination `Span<int>`/`Span<long>`, и reader заполняет его; размер destination обязан быть ≥ количеству элементов в потоке, иначе исключение в DEBUG.

## Валидация

Структурная валидация (контекст Compound/List, переполнение буфера и стека, рассогласование типов в List, длина строки ≤ 32767 байт, лишний Exit, lookup вне Compound) — только в `#if DEBUG`, через `[Conditional("DEBUG")]`-методы, которые JIT вырезает в Release. В горячем пути writer и reader доверяют caller'у. Исключения — `InvalidOperationException` с префиксом `$"[{nameof(NbtWriter)}] ..."` / `$"[{nameof(NbtReader)}] ..."`.

У reader'а есть второй класс ошибок: **чтение за пределами буфера** (битый поток, реальный EOF). Это не misuse, а испорченные данные — поэтому `EndOfStreamException` бросается всегда (не только в DEBUG), через явные проверки в raw-чтениях. Симметрия с обеими соседними слоями: как `NbtWriter` доверяет caller'у в Release, так и DataTypes из Network бросают `EndOfStreamException` при EOF.

## Текущие ограничения

- **DOM отложена.** Только потоковый writer/reader, без дерева узлов. Этого достаточно для Registry Data и чанков; DOM понадобится, если понадобится мутировать NBT-структуры inplace.
- **Только `Span<byte>`.** Перегрузка под `IBufferWriter<byte>` (как у DataTypes из Network) добавится, когда NBT понадобится в горячем пути с фрагментированной записью.
- **Векторизация `ModifiedUtf8` отложена.** Скалярный путь (с быстрым ASCII-детектом и widen byte→char) достаточен для имён тегов и идентификаторов; AVX2/SSE2 — когда чтение длинных строк станет hot-path'ом.
- **Zero-alloc reader.** Все операции `NbtReader` — без аллокаций: имена как byte-slice, строковые значения в `Span<char>`, скаляры в out-параметрах. Бенчмарки (`Verstack.NBT.Benchmark`) подтверждают `Allocated: 0` на всех кейсах (подтверждается повторным прогоном после оптимизации).
- **Разблокирует Registry Data в Gateway.** Пакет Registry Data (S→C 0x07) требует NBT — теперь writer и reader готовы для обоих режимов: listing-only (пустые тела) и full-content (полные тела из `Verstack.Vanilla`). См. [Gateway](../gateway/index.md).
- **`Verstack.Vanilla` — следующая задача.** Reader заложен под чтение ванильного датапака для сборки full-content Registry Data blob'ов; проект-хранилище данных 26.2 ещё не реализован.
