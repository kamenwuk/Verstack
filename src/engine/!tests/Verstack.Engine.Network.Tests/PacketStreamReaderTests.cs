using Verstack.Engine.Network.Packet.Readers;
using Verstack.Engine.Network.Packet;
using System.Text;

namespace Verstack.Engine.Network.Tests
{
    public class PacketStreamReaderTests
    {
        private static PacketStreamReader CreateReader(params byte[] data) => new PacketStreamReader(data, data.Length);

        /// <summary>
        /// Проверяет корректное чтение 32-битного значения переменной длины (VarInt).
        /// Сценарий: В буфере записано число 300 (0xAC 0x02). 
        /// Ожидание: Метод должен правильно собрать число из двух байт, сдвинуть указатель на 2 байта и не установить флаг ошибки.
        /// </summary>
        [Fact]
        public void ReadVarInt_ValidData_ReadsCorrectly()
        {
            var reader = CreateReader(0xAC, 0x02);
            
            int value = reader.ReadVarInt();
            
            Assert.False(reader.IsFaulted);
            Assert.Equal(300, value);
            Assert.Equal(2, reader.Offset);
        }

        /// <summary>
        /// Проверяет корректное чтение строки и её сравнение без аллокаций (через ReadOnlyUtf8Span).
        /// Сценарий: В буфере записана длина строки и байты строки "Hello".
        /// Ожидание: Метод ReadString должен вернуть валидную структуру, которая успешно сравнится с ожидаемыми байтами,
        /// а вызов ToString() вернёт полноценный объект string.
        /// </summary>
        [Fact]
        public void ReadString_ValidData_ReadsAndComparesCorrectly()
        {
            string expected = "Hello";
            byte[] strBytes = Encoding.UTF8.GetBytes(expected);
            byte[] data = new byte[1 + strBytes.Length];
            data[0] = (byte)strBytes.Length;
            Array.Copy(strBytes, 0, data, 1, strBytes.Length);

            var reader = CreateReader(data);
            
            ReadOnlyUtf8Span result = reader.ReadString();

            Assert.False(reader.IsFaulted);
            Assert.True(result.IsValid);
            Assert.True(result.Equals("Hello"u8));
            Assert.Equal(expected, result.ToString());
        }

        /// <summary>
        /// Проверяет безопасный вариант чтения строки через TryReadString при нехватке данных.
        /// Сценарий: В буфере записан VarInt, указывающий длину строки 10 байт, но самих байт в буфере нет.
        /// Ожидание: TryReadString должен вернуть false, структура должна быть невалидной (IsValid=false), 
        /// а ридер должен перейти в состояние ошибки (IsFaulted=true).
        /// </summary>
        [Fact]
        public void TryReadString_NotEnoughBytes_ReturnsFalseAndFaults()
        {
            byte[] data = { 0x0A }; // Длина 10
            var reader = CreateReader(data);

            bool success = reader.TryReadString(out ReadOnlyUtf8Span result);

            Assert.False(success);
            Assert.False(result.IsValid);
            Assert.True(reader.IsFaulted);
            Assert.Equal(0, result.Length);
        }

        /// <summary>
        /// Проверяет срабатывание паттерна "Отложенная ошибка" (Fault State) при нехватке данных.
        /// Сценарий: Буфер содержит всего 2 байта, но мы пытаемся прочитать Int32 (4 байта), а затем ещё один Byte.
        /// Ожидание: Ридер не должен выбросить исключение. Вместо этого он устанавливает IsFaulted=true, 
        /// возвращает значения по умолчанию (0) для обоих вызовов и блокирует дальнейшее чтение.
        /// </summary>
        [Fact]
        public void Read_WhenNotEnoughBytes_SetsFaultedAndReturnsDefault()
        {
            var reader = CreateReader(0x01, 0x02);

            int val = reader.ReadInt();
            byte b = reader.ReadByteRaw(); // Этот вызов должен скипнуться

            Assert.True(reader.IsFaulted);
            Assert.False(reader.IsValid);
            Assert.Equal(0, val);       // Default для int
            Assert.Equal(0, b);         // Default для byte
        }

        /// <summary>
        /// Проверяет, что после возникновения ошибки все последующие вызовы чтения мгновенно скипаются.
        /// Сценарий: Буфер содержит 1 байт. Сначала вызываем ReadLong (вызовет ошибку, т.к. нужно 8 байт).
        /// Затем пытаемся прочитать Span и VarInt.
        /// Ожидание: Последующие вызовы не должны обращаться к буферу. Они должны вернуть значения по умолчанию 
        /// (Empty Span и 0), а флаг IsFaulted должен оставаться true.
        /// </summary>
        [Fact]
        public void ReadOperations_AfterFault_SkipAndReturnDefaults()
        {
            var reader = CreateReader(0x01); // 1 байт

            reader.ReadLong(); // Вызовет ошибку (нужно 8 байт)
            var span = reader.ReadSpanRaw(10); // Должен вернуть Empty
            int num = reader.ReadVarInt(); // Должен вернуть 0

            Assert.True(reader.IsFaulted);
            Assert.Equal(ReadOnlySpan<byte>.Empty, span);
            Assert.Equal(0, num);
        }

        /// <summary>
        /// Проверяет чтение UUID (16 байт в Big-Endian формате).
        /// Сценарий: Генерируется случайный Guid, его байты записываются в буфер.
        /// Ожидание: Метод ReadUuid должен собрать Guid корректно, чтобы он совпадал с исходным.
        /// </summary>
        [Fact]
        public void ReadUuid_ValidData_ReadsCorrectly()
        {
            Guid expected = Guid.NewGuid();
            byte[] data = new byte[16];
            expected.TryWriteBytes(data, bigEndian: true, out _);

            var reader = CreateReader(data);
            Guid result = reader.ReadUuid();

            Assert.False(reader.IsFaulted);
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Проверяет чтение упакованного Vector3 (64 бита: 26 бит X, 26 бит Z, 12 бит Y).
        /// Сценарий: Координаты (X=10, Y=64, Z=-5) упаковываются в long и записываются в буфер.
        /// Ожидание: Метод должен правильно распаковать long, в том числе восстановить отрицательный знак у координаты Z.
        /// </summary>
        [Fact]
        public void ReadVector3_ValidData_ReadsCorrectly()
        {
            long value = ((long)10 & 0x3FFFFFF) << 38;
            value |= ((long)-5 & 0x3FFFFFF) << 12;
            value |= (long)64 & 0xFFF;

            byte[] data = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(data, value);

            var reader = CreateReader(data);
            var (x, y, z) = reader.ReadVector3();

            Assert.False(reader.IsFaulted);
            Assert.Equal(10, x);
            Assert.Equal(64, y);
            Assert.Equal(-5, z);
        }
    }
}