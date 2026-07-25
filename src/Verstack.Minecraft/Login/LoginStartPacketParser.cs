using Verstack.Protocol;

namespace Verstack.Minecraft.Login;

/// <summary>
/// Parses the body of a server-bound Login Start packet from a
/// <see cref="PacketReader"/> cursor. The reading-side counterpart to the
/// Login Start packet's wire format.
/// </summary>
/// <remarks>
/// Reads the packet BODY only — the packet id has already been consumed by the
/// caller (<see cref="Session.PacketDispatcher"/>) to route here.
/// <see cref="PACKET_ID"/> is published for the dispatcher's <c>switch</c> label,
/// not for this parser.
/// </remarks>
public static class LoginStartPacketParser
{
    /// <summary>Login Start packet id in the Login state (always 0x00).</summary>
    public const int PACKET_ID = 0x00;

    /// <summary>
    /// Reads a Login Start packet body from <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">Cursor positioned AFTER the packet id.</param>
    /// <param name="packet">Parsed packet on success; <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> if the whole body parsed; <see langword="false"/>
    /// otherwise (malformed packet — short read means a broken client, not «need more data»).</returns>
    public static bool TryParse(ref PacketReader reader, out LoginStartPacket packet)
    {
        // Поля идут подряд; любое непрочитанное → Malformed.
        if (!reader.TryReadString(out string? username)) goto Fail;
        if (!reader.TryReadUuid(out Uuid uuid)) goto Fail;

        // После успешного TryReadString username не null; ?? "" — для компилятора.
        packet = new LoginStartPacket(username ?? string.Empty, uuid);
        return true;

        Fail:
        packet = default;
        return false;
    }
}