using System.Runtime.CompilerServices;
using Verstack.Network.Packet.Readers;

namespace Verstack.Network.Packet
{
    /// <summary>
    /// Представляет разобранный сетевой пакет, готовый к обработке на уровне логики.
    /// </summary>
    /// <remarks>
    /// Структура содержит идентификатор пакета и его полезную нагрузку в виде массива байт.
    /// </remarks>
    public readonly struct RawPacket
    {
        /// <summary>
        /// Идентификатор типа пакета (Packet ID), извлечённый из заголовка.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Массив байт полезной нагрузки пакета.
        /// </summary>
        private readonly byte[] _data;

        internal RawPacket(int id, byte[] data)
        {
            Id = id;
            _data = data;
        }

        /// <summary>
        /// Создаёт экземпляр читателя для последовательного, безаллокационного чтения полезной нагрузки.
        /// </summary>
        /// <returns>
        /// Структуру <see cref="PacketStreamReader"/>, инициализированную данными этого пакета.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PacketStreamReader CreateReader() => new PacketStreamReader(_data, _data.Length);
    }
}