// using Verstack.Network.DataTypes;
// using Verstack.Network.Packet;
// using System.Buffers;
//
// namespace Verstack.Network.Tests;
//
// public sealed class SpanWriterTests
// {
//     [Fact]
//     public void WriteVarInt_SingleByteValue_WritesCorrectly()
//     {
//         byte[] buf = new byte[5];
//         var writer = new SpanWriter(buf);
//         VarInt.Write(ref writer, 42);
//         Assert.Equal(1, writer.Written);
//         Assert.Equal(42, buf[0] & 0x7F);
//     }
//
//     [Fact]
//     public void WriteVarInt_MultiByteValue_WritesCorrectly()
//     {
//         byte[] buf = new byte[5];
//         var writer = new SpanWriter(buf);
//         VarInt.Write(ref writer, 123456789);
//         Assert.Equal(4, writer.Written);
//     }
//
//     [Fact]
//     public void WriteUtf8String_ThenReadBack_ReturnsOriginal()
//     {
//         byte[] buf = new byte[256];
//         var writer = new SpanWriter(buf);
//         Utf8String.Write(ref writer, "minecraft:stone");
//         var writtenSpan = buf.AsSpan(0, writer.Written);
//
//         var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writtenSpan.ToArray()));
//         var result = Utf8String.Read(ref reader);
//         Assert.Equal("minecraft:stone", result);
//     }
//
//     [Fact]
//     public void WriteUtf8Bytes_ThenReadBack_ReturnsOriginal()
//     {
//         byte[] buf = new byte[256];
//         var writer = new SpanWriter(buf);
//         var original = "minecraft:stone"u8.ToArray();
//         Utf8String.Write(ref writer, original);
//         var writtenSpan = buf.AsSpan(0, writer.Written);
//
//         var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writtenSpan.ToArray()));
//         var result = Utf8String.Read(ref reader);
//         Assert.Equal("minecraft:stone", result);
//     }
// }
