# PacketFrameReader — чтение фреймов
> `src/Verstack.Protocol/PacketFrameReader.cs`

Читает кадры с VarInt-префиксом длины из последовательности байт.
Решает задачу фрейминга поверх TCP-потока: разбивает сплошной поток байт на отдельные пакеты Minecraft.

Поддерживает и итерацию через foreach, и чтение сжатых кадров (zlib).

## Сжатие
---

Если в конструктор передан IPacketDecompressor, ридер ожидает, что каждый кадр содержит VarInt(dataLength) после внешней длины.

- Если dataLength == 0: ридер отдаёт несжатый payload как есть (0 аллокаций).
- Если dataLength > 0: ридер арендует буфер через ArrayPool<byte>.Shared.Rent(dataLength), разжимает туда данные и отдаёт ReadOnlySequence<byte>, указывающую на этот буфер.

> Важно: так как ридер может арендовать память, он реализует IDisposable. Вызывающий код (например, SessionLifetime) должен использовать блок using или вызывать Dispose(), чтобы вернуть буфер в пул.

## Состояния
---

| Статус | Значение |
|--------|----------|
| `Complete` | Кадр прочитан (`MoveNext()` вернул `true`). |
| `Partial`  | Недостаточно данных — дождаться следующего `ReadAsync`. |
| `Malformed`| Бит продолжения в 5-м байте или длина больше лимита. Разорвать соединение. |

## API
---

```csharp
public ref struct PacketFrameReader : IDisposable
{
    public PacketFrameReader(ReadOnlySequence<byte> input, 
        int maxPacketSize = PacketFrameWriter.DefaultMaxPacketSize, 
        IPacketDecompressor? decompressor = null);

    public bool MoveNext();
    public ReadOnlySequence<byte> Current { get; }
    public VarInt.ReadStatus Status { get; }
    public SequencePosition ConsumedPosition { get; }
    public PacketFrameReader GetEnumerator();
    public void Dispose();
}
```

> Current — доступен только после успешного MoveNext().

> Status — причина, когда MoveNext() вернул false.

> ConsumedPosition — позиция для PipeReader.AdvanceTo. При Partial указывает на начало неполного кадра, чтобы его байты остались в буфере.

> GetEnumerator() — позволяет foreach (var frame in reader).

→ [Слой Protocol](index.md)