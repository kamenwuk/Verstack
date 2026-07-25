using Verstack.Minecraft.Login;
using Verstack.Protocol;
using System.Buffers;

namespace Verstack.Minecraft.Tests;

public class SetCompressionSerializerTests
{
    [Fact]
    public void Write_ProducesCorrectPayload()
    {
        var writer = new ArrayBufferWriter<byte>();
        SetCompressionSerializer.Write(writer, 256);

        var reader = new PacketPayloadReader(new ReadOnlySequence<byte>(writer.WrittenMemory));
        Assert.True(reader.TryReadVarInt(out int packetId));
        Assert.Equal(0x03, packetId);
        
        Assert.True(reader.TryReadVarInt(out int threshold));
        Assert.Equal(256, threshold);
    }
}