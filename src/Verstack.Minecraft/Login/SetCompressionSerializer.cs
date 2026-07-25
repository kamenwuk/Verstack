using System.Buffers;
using Verstack.Protocol;

namespace Verstack.Minecraft.Login;

/// <summary>
/// Serializes the Set Compression packet (client-bound 0x03 in Login state).
/// </summary>
/// <remarks>
/// Payload: [VarInt(Threshold)]. After this packet is sent, the framing
/// changes to the compressed format on both sides.
/// </remarks>
public static class SetCompressionSerializer
{
    /// <summary>Set Compression packet id in the Login state.</summary>
    public const int PACKET_ID = 0x03;

    /// <summary>
    /// Writes the Set Compression payload to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="threshold">Minimum payload size to compress. -1 to disable.</param>
    public static void Write(IBufferWriter<byte> output, int threshold)
    {
        // Формат payload: [VarInt(PACKET_ID)][VarInt(threshold)]
        Span<byte> span = output.GetSpan(VarInt.MAX_SIZE + VarInt.MAX_SIZE);
        int offset = 0;
        offset += VarInt.Encode(PACKET_ID, span[offset..]);
        offset += VarInt.Encode(threshold, span[offset..]);
        output.Advance(offset);
    }
}