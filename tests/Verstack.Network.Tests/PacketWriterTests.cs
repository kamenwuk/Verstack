using System;
using System.Text;
using Verstack.Network.Packet.Writers;
using Xunit;

namespace Verstack.Network.Tests.Packet
{
    public class PacketWriterTests
    {
        // Вспомогательный метод: используем out, так как ref struct нельзя класть в кортежи (ValueTuple)
        private static byte[] CreateBuffer(out PacketWriter writer, int size = 256)
        {
            var buffer = new byte[size];
            writer = new PacketWriter(buffer);
            return buffer;
        }

        // ─────────────────────────  VarInt  ─────────────────────────

        [Theory]
        [InlineData(0, new byte[] { 0x00 })]
        [InlineData(1, new byte[] { 0x01 })]
        [InlineData(127, new byte[] { 0x7F })]
        [InlineData(128, new byte[] { 0x80, 0x01 })]
        [InlineData(255, new byte[] { 0xFF, 0x01 })]
        [InlineData(2147483647, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 })]
        public void WriteVarInt_Writes_Correct_Bytes(int value, byte[] expected)
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteVarInt(value);

            Assert.Equal(expected, buffer[..writer.Written]);
        }

        [Fact]
        public void WriteVarLong_Writes_Correct_Bytes()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteVarLong(2147483648L); // 2^31

            byte[] expected = { 0x80, 0x80, 0x80, 0x80, 0x08 };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        // ─────────────────────────  Числа (Big-Endian)  ─────────────────────────

        [Fact]
        public void WriteShort_Writes_BigEndian()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteShort(-1);

            Assert.Equal(new byte[] { 0xFF, 0xFF }, buffer[..writer.Written]);
        }

        [Fact]
        public void WriteInt_Writes_BigEndian()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteInt(1);

            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x01 }, buffer[..writer.Written]);
        }

        [Fact]
        public void WriteLong_Writes_BigEndian()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteLong(1);

            byte[] expected = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        [Fact]
        public void WriteFloat_Writes_BigEndian()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteFloat(1.0f); // IEEE 754: 0x3F800000

            byte[] expected = { 0x3F, 0x80, 0x00, 0x00 };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        // ─────────────────────────  Булевые значения  ─────────────────────────

        [Theory]
        [InlineData(true, new byte[] { 0x01 })]
        [InlineData(false, new byte[] { 0x00 })]
        public void WriteBool_Writes_Correct_Byte(bool value, byte[] expected)
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteBool(value);

            Assert.Equal(expected, buffer[..writer.Written]);
        }

        // ─────────────────────────  Строки  ─────────────────────────

        [Fact]
        public void WriteString_Writes_Length_Prefix_And_Utf8()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteString("ABC");

            byte[] expected = { 0x03, 0x41, 0x42, 0x43 };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        [Fact]
        public void WriteString_FromSpan_Writes_Length_Prefix_And_RawBytes()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteString("minecraft:overworld"u8);

            byte[] expectedPrefix = { 0x13 }; // 19 в десятичной
            ReadOnlySpan<byte> expectedString = "minecraft:overworld"u8;
            
            Assert.Equal(expectedPrefix, buffer[..1]);
            Assert.True(buffer[1..(1 + expectedString.Length)].SequenceEqual(expectedString));
            Assert.Equal(1 + expectedString.Length, writer.Written);
        }

        // ─────────────────────────  UUID  ─────────────────────────

        [Fact]
        public void WriteUuid_Writes_BigEndian_Rfc4122()
        {
            var buffer = CreateBuffer(out var writer);
            var guid = Guid.Parse("01020304-0506-0708-090a-0b0c0d0e0f10");
            
            writer.WriteUuid(guid);

            byte[] expected = 
            { 
                0x01, 0x02, 0x03, 0x04, 
                0x05, 0x06, 
                0x07, 0x08, 
                0x09, 0x0a, 
                0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10 
            };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        // ─────────────────────────  Векторы  ─────────────────────────

        [Fact]
        public void WriteVector3_Packs_Correctly()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteVector3(1, 2, 3); // X=1, Y=2, Z=3

            byte[] expected = { 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x30, 0x02 };
            Assert.Equal(expected, buffer[..writer.Written]);
        }

        // ─────────────────────────  Fluent API (Цепочки)  ─────────────────────────

        [Fact]
        public void FluentChain_Writes_Multiple_Types_In_Order()
        {
            var buffer = CreateBuffer(out var writer);
            
            writer.WriteVarInt(0x31)        // 1 байт
                  .WriteInt(100)            // 4 байта
                  .WriteBool(true)          // 1 байт
                  .WriteString("A");        // 2 байта (1 длина + 1 символ)

            // Итого 8 байт
            Assert.Equal(8, writer.Written);

            byte[] expected = 
            { 
                0x31,                               // VarInt 0x31
                0x00, 0x00, 0x00, 0x64,             // Int 100
                0x01,                               // Bool true
                0x01, 0x41                          // String "A"
            };
            Assert.Equal(expected, buffer[..writer.Written]);
        }
    }
}