using System.Runtime.CompilerServices;

namespace Verstack.Network.Packet.Writers
{
    /// <summary>
    /// Предоставляет быстрый, безаллокационный буфер для последовательной записи байт.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Структура работает поверх массива, арендованного из пула (<c>ArrayPool</c>). 
    /// При нехватке места буфер автоматически расширяется: арендуется новый массив большего размера, 
    /// данные копируются, а старый массив возвращается в пул.
    /// </para>
    /// <para>
    /// Является <c>ref struct</c>, что исключает её упаковку (boxing) и гарантирует работу исключительно на стеке,
    /// обеспечивая максимальную производительность и отсутствие нагрузки на сборщик мусора (GC).
    /// </para>
    /// </remarks>
    public ref struct PacketStreamWriter
    {
        /// <summary>
        /// Количество байт, записанных в буфер на данный момент.
        /// </summary>
        public int Written => Offset;
        
        /// <summary>
        /// Диапазон только для чтения, представляющий записанные данные.
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan => Buffer.AsSpan(0, Offset);
        
        /// <summary>
        /// Текущий арендованный массив-буфер. 
        /// Может быть заменён на новый при автоматическом расширении.
        /// </summary>
        internal byte[] Buffer;
        
        /// <summary>
        /// Текущая позиция записи (смещение от начала буфера).
        /// </summary>
        internal int Offset = 0;

        /// <summary>
        /// Диапазон, доступный для записи начиная с текущей позиции.
        /// </summary>
        internal Span<byte> FreeSpan => Buffer.AsSpan(Offset);

        /// <summary>
        /// Инициализирует новый экземпляр писателя поверх указанного буфера.
        /// </summary>
        /// <param name="buffer">Массив-буфер для записи данных.</param>
        /// <param name="offset">Начальное смещение в буфере.</param>
        internal PacketStreamWriter(byte[] buffer, int offset = 0)
        {
            Buffer = buffer;
            Offset = offset;
        }

        /// <summary>
        /// Гарантирует, что в буфере достаточно места для записи указанного количества байт.
        /// При необходимости расширяет буфер.
        /// </summary>
        /// <param name="count">Требуемое количество байт для записи.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int count)
        {
            if (Offset + count > Buffer.Length)
            {
                int newSize = Buffer.Length;
                // Защита от бесконечного цикла при нулевой длине
                if (newSize == 0) newSize = 64;
                
                while (newSize < Offset + count)
                    newSize *= 2;

                var newBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(newSize);
                Buffer.AsSpan(0, Offset).CopyTo(newBuffer);
                System.Buffers.ArrayPool<byte>.Shared.Return(Buffer);
                Buffer = newBuffer;
            }
        }

        /// <summary>
        /// Продвигает указатель записи вперёд на заданное количество байт.
        /// </summary>
        /// <param name="count">Количество записанных байт.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Advance(int count) => Offset += count;

        /// <summary>
        /// Сбрасывает позицию записи в начало, позволяя переиспользовать буфер без его очистки.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reset() => Offset = 0;
    }
}