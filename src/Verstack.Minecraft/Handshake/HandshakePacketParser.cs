using Verstack.Protocol;

namespace Verstack.Minecraft.Handshake;

/// <summary>
/// Parses the body of a serverbound Handshake packet from a
/// <see cref="PacketPayloadReader"/> cursor. The reading-side counterpart to the
/// Handshake packet's wire format.
/// </summary>
/// <remarks>
/// Reads the packet BODY only — the packet id has already been consumed by the
/// caller (<c>ConnectionHandler</c> dispatcher) to route here. <see cref="PACKET_ID"/>
/// is published for the dispatcher's <c>switch</c> label, not for this parser.
/// </remarks>
public static class HandshakePacketParser
{
    /// <summary>Handshake packet id in the Handshake state (always 0x00).</summary>
    public const int PACKET_ID = 0x00;

    /// <summary>
    /// Reads a Handshake packet body from <paramref name="payloadReader"/>.
    /// </summary>
    /// <param name="payloadReader">Cursor positioned AFTER the packet id.</param>
    /// <param name="packet">Parsed packet on success; <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> if the whole body parsed and
    /// <see cref="NextState"/> is a valid protocol value; <see langword="false"/>
    /// otherwise (malformed packet).</returns>
    public static bool TryParse(ref PacketPayloadReader payloadReader, out HandshakePacket packet)
    {
        // Поля идут подряд; любое непрочитанное поле → Malformed.
        // goto Fail вместо серии ранних return'ов с дублированием packet = default.
        if (!payloadReader.TryReadVarInt(out int protocolVersion)) goto Fail;
        if (!payloadReader.TryReadString(out string? serverAddress)) goto Fail;
        if (!payloadReader.TryReadUShortBigEndian(out ushort serverPort)) goto Fail;
        if (!payloadReader.TryReadVarInt(out int nextStateRaw)) goto Fail;

        // nextState валидируется на границе парсинга: протокол предусматривает
        // только Status(1) и Login(2). Любое другое значение — кривой клиент.
        if (nextStateRaw != (int)HandshakeNextState.Status
            && nextStateRaw != (int)HandshakeNextState.Login)
        {
            goto Fail;
        }

        // После успешного TryReadString serverAddress не null; ?? "" — для компилятора.
        packet = new HandshakePacket(
            protocolVersion,
            serverAddress ?? string.Empty,
            serverPort,
            (HandshakeNextState)nextStateRaw);
        return true;

    Fail:
        packet = default;
        return false;
    }
}