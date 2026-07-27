using System.Buffers;

namespace Verstack.Network.Packet
{
    /// <summary>
    /// Универсальный конвейер пакетов. Работает как конечный автомат.
    /// </summary>
    public sealed class PacketPipeline
    {
        private readonly PacketBundle[] _bundles;

        public PacketPipeline(PacketBundle[] bundles)
        {
            _bundles = bundles;
            
            // Выставляем индексы бандлам
            for (var idx = 0; idx < _bundles.Length; idx++)
                _bundles[idx].Index = idx;
        }

        /// <summary>
        /// Запускает пакет через текущий бандл.
        /// </summary>
        public bool TryProcessPacket(in RawPacket packet, IBufferWriter<byte> writer, ref PacketFlowState state)
        {
            if (state.BundleIndex < 0 || state.BundleIndex >= _bundles.Length)
                return false;

            var bundle = _bundles[state.BundleIndex];
            return bundle.TryProcess(packet, writer, ref state);
        }
    }
}