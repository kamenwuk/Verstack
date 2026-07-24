namespace Verstack.Minecraft.Handshake;

/// <summary>
/// Client-requested next state after Handshake: the <c>nextState</c> field of
/// the server-bound Handshake packet. Mirrors the wire values (1 for Status,
/// 2 for Login).
/// </summary>
/// <remarks>
/// Distinct from <c>ConnectionState</c>: this enum captures what the protocol
/// allows the client to ask for, not what the server implements.
/// </remarks>
public enum HandshakeNextState
{
    /// <summary>Transition to the Status phase (server list ping).</summary>
    Status = 1,

    /// <summary>Transition to the Login phase (full game join).</summary>
    Login = 2,
}