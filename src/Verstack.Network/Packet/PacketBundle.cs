using System.Buffers;

namespace Verstack.Network.Packet;

public abstract class PacketBundle
{
    public int Index { get; internal set; }
    
    /// <summary>
    /// Обрабатывает пакет.
    /// </summary>
    /// <param name="packet">Сырой пакет от клиента</param>
    /// <param name="writer">Буфер для записи ответа клиенту</param>
    /// <param name="state">Текущее состояние конвейера (можно менять)</param>
    /// <returns>True, если пакет валиден. False — кик.</returns>
    public abstract bool TryProcess(in RawPacket packet, IBufferWriter<byte> writer, ref PacketFlowState state);
}