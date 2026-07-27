using System.Buffers;

namespace Verstack.Network.Packet;

/// <summary>
/// Порция байтов, подготовленная для отправки клиенту. Деталь реализации <see cref="Network.NetworkChannel"/>:
/// слои работают через <c>channel.EnqueueOutbound(ReadOnlySpan&lt;byte&gt;)</c> и не знают про этот тип.
///
/// Буфер взят из <c>ArrayPool&lt;byte&gt;</c> и возвращается send-воркером после записи в <c>PipeWriter</c>.
/// <see cref="Length"/> может быть меньше длины массива — арендованный буфер часто больше записанных данных.
/// </summary>
internal readonly struct OutboundChunk
{
    internal readonly byte[] Buffer;
    internal readonly int Length;

    private OutboundChunk(byte[] buffer, int length)
    {
        Buffer = buffer;
        Length = length;
    }

    /// <summary>
    /// Арендует буфер из <c>ArrayPool&lt;byte&gt;</c> и копирует в него <paramref name="source"/>.
    /// </summary>
    internal static OutboundChunk Rent(ReadOnlySpan<byte> source)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length);
        source.CopyTo(buffer);
        return new OutboundChunk(buffer, source.Length);
    }
}
