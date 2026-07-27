namespace Verstack.Layer.Gateway;

internal readonly struct NetworkSession(int protocolVersion, string ipAddress, string serverAddress, ushort serverPort)
{
    public readonly int ProtocolVersion = protocolVersion;
    public readonly string IpAddress = ipAddress;
    public readonly string ServerAddress = serverAddress;
    public readonly ushort ServerPort = serverPort;
}