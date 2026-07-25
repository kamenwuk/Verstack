# Compression — сжатие пакетов
> src/Verstack.Protocol/IPacketCompressor.cs

> src/Verstack.Protocol/ZLibPacketCompressor.cs

После фазы Login (пакет Set Compression) Minecraft переключает фрейминг на сжатый формат (RFC 1950 / zlib). 
Слой Protocol абстрагирует алгоритм сжатия через интерфейсы, чтобы реализацию можно было подменить (например, на P/Invoke к нативному `zlib-ng`).

## Интерфейсы
---

**IPacketCompressor**, используется PacketFrameWriter при записи исходящих пакетов.

``` csharp
int GetMaxCompressedSize(int sourceLength) — возвращает верхнюю оценку размера сжатых данных. Нужна, чтобы фреймворк мог зарезервировать слитный кусок памяти (Span<byte>) в IBufferWriter до начала сжатия.
int Compress(ReadOnlySpan<byte> source, Span<byte> destination) — сжимает source в destination, возвращает число записанных байт.
```

**IPacketDecompressor**, используется PacketFrameReader при чтении входящих пакетов.

``` csharp
void Decompress(ReadOnlySequence<byte> source, Span<byte> destination) — разжимает данные. Размер destination всегда точно равен dataLength из заголовка кадра.
```

## Реализации по умолчанию
---

`ZLibPacketCompressor` и `ZLibPacketDecompressor` используют встроенный в BCL `System.IO.Compression.ZLibStream`, который под капом вызывает нативный `zlib-ng`.
Для минимизации аллокаций (так как Stream-API требует `byte[]`) реализации арендуют временные буферы через `ArrayPool<byte>.Shared`.

→ [Protocol layer](index.md)