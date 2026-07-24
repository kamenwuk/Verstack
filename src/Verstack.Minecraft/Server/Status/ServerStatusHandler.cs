using System.IO.Pipelines;
using Verstack.Protocol;
using Verstack.Network;
using System.Buffers;

namespace Verstack.Minecraft.Status;

/// <summary>
/// Stub handler for the Status phase: replies with a
/// <see cref="ServerStatusResponse"/> to ANY incoming frame.
/// </summary>
/// <remarks>
/// Intentional shortcut to reach a visual win (MOTD in the server list)
/// without parsing Handshake or tracking protocol state. A real dispatcher
/// — Handshake parser, state machine, per-packet-id routing — is a separate
/// milestone; this stub is replaced then.
///
/// Why a scratch buffer: <see cref="ServerStatusSerializer"/> writes
/// the packet PAYLOAD, while <see cref="PacketFraming"/> wraps it in a length
/// prefix. The framing needs the payload as a contiguous span, but the payload
/// is already written into the writer — so we serialize the payload into a
/// temporary buffer first, then frame it into the connection. One allocation
/// per ping (low frequency); pooled via <c>ArrayPool</c> later.
/// </remarks>
public sealed class ServerStatusHandler : IPacketHandler
{
    private readonly ServerStatusResponse _status;

    /// <param name="status">Server status data to send on every packet.</param>
    public ServerStatusHandler(ServerStatusResponse status)
    {
        _status = status;
    }

    /// <inheritdoc/>
    public void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output)
    {
        // Stub: игнорируем содержимое payload — отвечаем статусом на любой кадр.
        _ = payload;

        // Фаза 1: payload пакета (packetId + jsonLen + JSON) во временный буфер.
        var scratch = new ArrayBufferWriter<byte>();
        ServerStatusSerializer.Write(scratch, in _status);

        // Фаза 2: обернуть payload в кадр [VarInt(len)][payload] и записать в Pipe.
        PacketFraming.Write(output, scratch.WrittenSpan);
    }
}