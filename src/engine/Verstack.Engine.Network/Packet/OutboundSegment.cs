using System.Buffers;

namespace Verstack.Engine.Network.Packet;

/// <summary>
/// Порция байтов, подготовленная для отправки клиенту. Деталь реализации <see cref="Network.NetworkChannel"/>:
/// слои работают через <c>channel.EnqueueOutbound(ReadOnlySpan&lt;byte&gt;)</c> и не знают про этот тип.
///
/// Буфер взят из <c>ArrayPool&lt;byte&gt;</c> и возвращается send-воркером после записи в <c>PipeWriter</c>.
/// <see cref="Length"/> может быть меньше длины массива — арендованный буфер часто больше записанных данных.
/// </summary>
internal readonly struct OutboundSegment
{
    public readonly byte[] Buffer;
    public readonly int Length;

    private OutboundSegment(byte[] buffer, int length)
    {
        Buffer = buffer;
        Length = length;
    }

    public static OutboundSegment Rent(ReadOnlySpan<byte> data)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
        data.CopyTo(buffer);
        return new OutboundSegment(buffer, data.Length);
    }

    public static OutboundSegment FromRentedArray(byte[] rentedBuffer, int length)
    {
        return new OutboundSegment(rentedBuffer, length);
    }
}