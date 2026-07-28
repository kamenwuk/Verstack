using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Verstack.Layer.Global;
using System.Buffers;
using Verstack.Debug;

namespace Verstack.Layer.Gateway
{
    public class GatewayIntakeHandler
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
    }
}