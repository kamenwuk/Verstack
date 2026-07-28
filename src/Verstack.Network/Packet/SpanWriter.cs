using System.Buffers;

namespace Verstack.Network.Packet;

/// <summary>
/// Адаптер <c>Span&lt;byte&gt;</c> для GC-free записи framing'а и payload в арендованный/стековый буфер.
///
/// ref struct не может реализовать <see cref="IBufferWriter{T}"/>, поэтому DataType'ы имеют по две
/// перегрузки Write: под <c>IBufferWriter&lt;byte&gt;</c> и под <c>ref SpanWriter</c>. Дублирование —
/// осознанная плата за GC-free на ref struct (нельзя обобщить через интерфейс).
/// </summary>
public ref struct SpanWriter(Span<byte> buffer)
{
    private readonly Span<byte> _buffer = buffer;
    private int _offset = 0;

    public int Written => _offset;

    public Span<byte> GetSpan(int sizeHint) => _buffer[_offset..];

    public void Advance(int count) => _offset += count;

    public ReadOnlySpan<byte> WrittenSpan => _buffer[.._offset];
}