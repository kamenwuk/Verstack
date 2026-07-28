using System.Buffers;
using Leopotam.EcsProto;

namespace Verstack.Network.Packet
{
    /// <summary>
    /// Универсальный конвейер пакетов. Работает как конечный автомат.
    /// </summary>
    public sealed class PacketPipeline
    {
        private readonly PacketBundle[] _bundles;

        public PacketPipeline(IProtoSystems systems, PacketBundle[] bundles)
        {
            _bundles = bundles;
            
            // Выставляем индексы бандлам
            for (var idx = 0; idx < _bundles.Length; idx++)
            {
                _bundles[idx].Index = idx;
                _bundles[idx].Init(systems);
            }
        }

        /// <summary>
        /// Запускает пакет через текущий бандл.
        /// </summary>
        public bool TryProcessPacket(ProtoEntity entity, in RawPacket packet, IBufferWriter<byte> writer, ref PacketFlowState state)
        {
            if (state.BundleIndex < 0 || state.BundleIndex >= _bundles.Length)
                return false;
            
            var bundle = _bundles[state.BundleIndex];
            if (!bundle.TryProcess(state.StepIndex, entity, packet, writer))
                return false;
            
            state.StepIndex++;
            if (state.StepIndex >= bundle.StepCount)
            {
                state.BundleIndex++;
                state.StepIndex = 0;
            }
            return true;
        }
    }
}