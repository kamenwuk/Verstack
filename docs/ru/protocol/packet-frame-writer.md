# PacketFrameWriter — запись фреймов
> `src/Verstack.Protocol/PacketFrameWriter.cs`

Записывает кадры с VarInt-префиксом длины в `IBufferWriter<byte>`.
Противоположность `PacketFrameReader`: оборачивает payload длиной и добавляет целый кадр в буфер.

Поддерживает два режима:

1. Без сжатия (по умолчанию): [VarInt(len)][payload].
2. Со сжатием: включается, если передан IPacketCompressor и compressionThreshold >= 0. Формат кадра меняется на [VarInt(packetLength)][VarInt(dataLength)][payload | compressedPayload].

## Логика сжатия
---

Если сжатие включено, решение принимается по размеру payload:

* payload.Length < threshold: кадр не сжимается. dataLength пишется как 0, далее идёт сырой payload.
* payload.Length >= threshold: кадр сжимается через IPacketCompressor. dataLength пишется как payload.Length, далее идёт сжатый буфер.

Для оценки требуемого размера буфера PacketFrameWriter запрашивает у компрессора GetMaxCompressedSize(), после чего получает слитный Span<byte> от IBufferWriter и пишет заголовки и данные без лишних аллокаций.

## API
---

```csharp
public static class PacketFrameWriter
{
    public const int DefaultMaxPacketSize = 2 * 1024 * 1024;

    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload, 
        IPacketCompressor? compressor = null, int compressionThreshold = -1);
        {
        }
```

> DefaultMaxPacketSize — стандартный лимит размера кадра (2 МБ). Используется и в PacketFrameReader.

> Encode — записывает кадр в output. Если compressor равен null или compressionThreshold < 0, сжатие не применяется.

→ [Protocol layer](index.md)