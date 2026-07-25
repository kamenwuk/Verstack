# PacketFrameWriter — запись фреймов
> `src/Verstack.Protocol/PacketFrameWriter.cs`

Записывает кадры с VarInt-префиксом длины в `IBufferWriter<byte>`.
Противоположность `PacketFrameReader`: оборачивает payload длиной и добавляет целый кадр в буфер.

## API
---

```csharp
public static class PacketFrameWriter
{
    public const int DefaultMaxPacketSize = 2 * 1024 * 1024;

    public static void Encode(IBufferWriter<byte> output, ReadOnlySpan<byte> payload);
}
```

> DefaultMaxPacketSize — стандартный лимит размера кадра (2 МБ). Используется и в PacketFrameReader.

> Encode — записывает [VarInt(длина)][payload] в output одним непрерывным span'ом.

Перечисление статусов не требуется: запись в буфер всегда успешна (или выбрасывает исключение при превышении лимита).

→ [Protocol layer](index.md)