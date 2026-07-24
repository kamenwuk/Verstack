namespace Verstack.Minecraft.Handshake;

/// <summary>
/// Parsed server-bound Handshake packet — the first frame a client sends on
/// connect. Carries the protocol version it speaks, the address it connected
/// to, the port, and the state it wants to transition to.
/// </summary>
public readonly struct HandshakePacket(int protocolVersion, string serverAddress, ushort serverPort, HandshakeNextState nextState)
{
    /// <summary>Protocol version the client speaks (e.g. <c>774</c> for 1.21.6).</summary>
    public readonly int ProtocolVersion = protocolVersion;

    /// <summary>Host string the client connected to (typically the server hostname).</summary>
    public readonly string ServerAddress = serverAddress;

    /// <summary>Port the client connected to (informational; not the actual port).</summary>
    public readonly ushort ServerPort = serverPort;

    /// <summary>State the client requests to transition to.</summary>
    public readonly HandshakeNextState NextState = nextState;
}