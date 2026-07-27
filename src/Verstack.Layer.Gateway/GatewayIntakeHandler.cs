using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Verstack.Layer.Global;
using System.Buffers;
using Verstack.Debug;

namespace Verstack.Layer.Gateway
{
    public class GatewayIntakeHandler(ServerInfoCacheStore serverInfoCacheStore)
    {
        /// <summary>
        /// Парсит первый пакет (Handshake) от клиента.
        /// </summary>
        /// <returns>1 - Status, 2 - Login, -1 - Ошибка (Кик)</returns>
        public int TryParseHandshake(in RawPacket packet, out (int protocolVersion, string serverAddress, ushort serverPort) data)
        {
            data.protocolVersion = 0;
            data.serverAddress = string.Empty;
            data.serverPort = 0;
            
            if (packet.Id != 0x00) 
                return -1;
            
            try
            {
                var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));

                int protocolVersion = VarInt.Read(ref reader);
                string serverAddress = Utf8String.Read(ref reader);
                ushort serverPort = Numeric.ReadUShort(ref reader);
                int nextState = VarInt.Read(ref reader);

                if (nextState != 1 && nextState != 2)
                    return -1;
                
                data.protocolVersion = protocolVersion;
                data.serverAddress = serverAddress;
                data.serverPort = serverPort;
                return nextState;
            }
            catch (EndOfStreamException)
            {
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Обрабатывает пакеты пингования (Status Request / Ping) и пишет ответ в буфер.
        /// </summary>
        /// <returns>True, если пакет валиден. False, если кикаем.</returns>
        public bool TryHandleStatusRequest(in RawPacket packet, IBufferWriter<byte> writer)
        {
            if (packet.Id == 0x00)
            {
                Logger.Debug(LogKey.PacketStatusRequest);
                
                byte[] jsonBytes = serverInfoCacheStore.GetStatusJson();

                // 1. Считаем длину того, что идет ПОСЛЕ длины пакета (ID + Длина строки + JSON)
                int payloadLength = 
                    VarInt.GetSize(0x00) +               // Размер ID пакета (0x00 = 1 байт)
                    VarInt.GetSize(jsonBytes.Length) +   // Размер VarInt длины строки
                    jsonBytes.Length;                    // Сама строка

                // 2. Пишем Длину пакета (твой метод из DataTypes)
                VarInt.Write(writer, payloadLength);
                
                // 3. Пишем ID пакета
                VarInt.Write(writer, 0x00);

                // 4. Пишем длину строки (по протоколу Minecraft так передается JSON в Status)
                VarInt.Write(writer, jsonBytes.Length);

                // 5. Пишем сам JSON напрямую в буфер
                jsonBytes.CopyTo(writer.GetSpan(jsonBytes.Length));
                writer.Advance(jsonBytes.Length);

                return true;
            }
            
            if (packet.Id == 0x01)
            {
                Logger.Debug(LogKey.PacketPingRequest);
                try
                {
                    var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(packet.Data));
                    if (reader.TryReadBigEndian(out long payload))
                    {
                        // В Ping ответе payload = 1 байт (ID) + 8 байт (Long) = 9 байт
                        VarInt.Write(writer, 9);        // Длина пакета
                        VarInt.Write(writer, 0x01);     // ID пакета
                        Numeric.WriteLong(writer, payload); // Твой метод из DataTypes
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}