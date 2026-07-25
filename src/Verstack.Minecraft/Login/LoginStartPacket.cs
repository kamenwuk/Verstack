using Verstack.Protocol;

namespace Verstack.Minecraft.Login;

/// <summary>
/// Parsed server-bound Login Start packet (0x00 in the Login state) — the first
/// frame a client sends after Handshake with <c>nextState = Login</c>. Carries
/// the username the client claims and a UUID it generated.
/// </summary>
/// <remarks>
/// No authentication data: online-mode verification happens later via the
/// Encryption Request / Encryption Response exchange (and a Mojang
/// <c>hasJoined</c> call), not in this packet.
/// </remarks>
public readonly struct LoginStartPacket(string username, Uuid uuid)
{
    /// <summary>Username the client claims (max 16 chars per protocol; not enforced here).</summary>
    public readonly string Username = username;

    /// <summary>UUID the client generated for itself; overwritten by the server's
    /// UUID on Login Success in online mode.</summary>
    public readonly Uuid Uuid = uuid;
}