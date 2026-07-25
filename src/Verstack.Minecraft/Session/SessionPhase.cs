namespace Verstack.Minecraft.Session;

/// <summary>
/// Phase of a Minecraft protocol session: which packet-id space is currently
/// in effect. A session starts in <see cref="Handshake"/> and transitions
/// based on the <c>nextState</c> field of the Handshake packet.
/// </summary>
/// <remarks>
/// Ordered so that <see cref="Handshake"/> (value <c>0</c>) is the
/// <see langword="default"/> — a freshly created dispatcher begins in the
/// correct phase without an explicit initializer.
/// </remarks>
public enum SessionPhase
{
    /// <summary>Initial phase: the client sends a Handshake to negotiate the
    /// protocol version and the next phase.</summary>
    Handshake,

    /// <summary>Status phase: server-list ping (Status Request/Response, Ping/Pong).</summary>
    Status,
    
    /// <summary>Login phase: client sent Login Start; server will negotiate
    /// encryption, compression, then send Login Success.</summary>
    Login,
}