namespace Verstack.Network;

/// <summary>
/// Verdict returned by <see cref="IPacketHandler.OnPacket"/> to tell
/// <c>SessionLifetime</c> what to do with the connection after this frame.
/// </summary>
/// <remarks>
/// <see cref="Keep"/> is the default and the common case: the frame was
/// processed (or safely ignored) and the connection stays open. The handler
/// returns <see cref="Disconnect"/> when the frame is unrecoverable garbage
/// (e.g. a valid frame whose payload does not parse as any expected packet)
/// and the connection should be torn down.
/// </remarks>
public enum PacketVerdict
{
    /// <summary>Keep the connection open and continue the read loop. Default.</summary>
    Keep = 0,

    /// <summary>Tear the connection down after this frame.</summary>
    Disconnect = 1,
}