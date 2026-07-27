using Verstack.Network.DataTypes;
using System.Buffers;

namespace Verstack.Network.Packet;

public static class PacketWriter
{
    /// <summary>
    /// Записывает пакет в буфер с автоматическим расчетом длины.
    /// </summary>
    public static void WritePacket(IBufferWriter<byte> writer, int packetId, Action<IBufferWriter<byte>> writeData)
    {
        // 1. Сначала пишем данные во временный буфер, чтобы узнать их длину
        var tempWriter = new ArrayBufferWriter<byte>();
        VarInt.Write(tempWriter, packetId); // ID пакета
        writeData(tempWriter);              // Payload (данные)

        // 2. Пишем в основной буфер ДЛИНУ всего пакета (ID + Data)
        VarInt.Write(writer, tempWriter.WrittenCount);
        
        // 3. Копируем сам пакет (ID + Data) в основной буфер
        tempWriter.WrittenSpan.CopyTo(writer.GetSpan(tempWriter.WrittenCount));
        writer.Advance(tempWriter.WrittenCount);
    }
}