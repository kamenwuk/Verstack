using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Engine.Network.Packet;

namespace Verstack.Layers.Gateway
{
    internal static class GatewayIntakeHandler
    {
        /// <summary>
        /// Парсит первый пакет (Handshake) от клиента.
        /// </summary>
        /// <returns>1 - Status, 2 - Login, -1 - Ошибка (Кик)</returns>
        public static int TryParseHandshake(in RawPacket packet, out (int protocolVersion, string serverAddress, ushort serverPort) data)
        {
            data.protocolVersion = 0;
            data.serverAddress = string.Empty;
            data.serverPort = 0;
            
            if (packet.Id != 0x00) 
                return -1;
            
            try
            {
                
                var reader = packet.CreateReader();

                int protocolVersion = reader.ReadVarInt();
                ReadOnlyUtf8Span serverAddress = reader.ReadString();
                ushort serverPort = reader.ReadUShort();
                int nextState = reader.ReadVarInt();
                
                if (reader.IsFaulted)
                    return -1;
                
                if (nextState != 1 && nextState != 2)
                    return -1;
                
                data.protocolVersion = protocolVersion;
                data.serverAddress = serverAddress.ToString();
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
    }
}