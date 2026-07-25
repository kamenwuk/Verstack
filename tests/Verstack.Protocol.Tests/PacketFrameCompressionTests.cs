using System.Buffers;
using System.Text;

namespace Verstack.Protocol.Tests;

public class PacketFrameCompressionTests
{
    [Fact]
    public void Encode_CompressesWhenAboveThreshold()
    {
        var compressor = new ZLibPacketCompressor();
        var decompressor = new ZLibPacketDecompressor();
        var writer = new ArrayBufferWriter<byte>();
        var payload = Encoding.UTF8.GetBytes("This is a test payload that is long enough to be compressed. Blah blah blah blah blah.");

        // Сжимаем с threshold=10 (payload точно больше)
        PacketFrameWriter.Encode(writer, payload, compressor, 10);

        // Читаем сжатый кадр
        var reader = new PacketFrameReader(new ReadOnlySequence<byte>(writer.WrittenMemory), decompressor: decompressor);
        using (reader)
        {
            Assert.True(reader.MoveNext());
            Assert.Equal(payload, reader.Current.ToArray());
            Assert.False(reader.MoveNext());
        }
    }

    [Fact]
    public void Encode_DoesNotCompressWhenBelowThreshold()
    {
        var compressor = new ZLibPacketCompressor();
        var decompressor = new ZLibPacketDecompressor();
        var writer = new ArrayBufferWriter<byte>();
        var payload = Encoding.UTF8.GetBytes("short");

        // Сжимаем с threshold=100 (payload меньше)
        PacketFrameWriter.Encode(writer, payload, compressor, 100);

        var reader = new PacketFrameReader(new ReadOnlySequence<byte>(writer.WrittenMemory), decompressor: decompressor);
        using (reader)
        {
            Assert.True(reader.MoveNext());
            Assert.Equal(payload, reader.Current.ToArray());
            Assert.False(reader.MoveNext());
        }
    }
}