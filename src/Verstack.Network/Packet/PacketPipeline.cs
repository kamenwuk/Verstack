using System.Buffers;
using Leopotam.EcsProto;

namespace Verstack.Network.Packet
{
    /// <summary>
    /// Универсальный конвейер пакетов. Работает как конечный автомат.
    /// </summary>
    public sealed class PacketPipeline
    {
        /// <summary>
        /// Число бандлов. Выход <see cref="PacketFlowState.BundleIndex"/> за этот предел
        /// означает, что конвейер прошёл все фазы — соединение обработано до конца.
        /// </summary>
        public int BundleCount => _bundles.Length;
        
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
        public PacketHandleResult TryProcessPacket(ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound, ref PacketFlowState state)
        {
            if (state.BundleIndex < 0 || state.BundleIndex >= _bundles.Length)
                return PacketHandleResult.Kick;

            var bundle = _bundles[state.BundleIndex];
            var result = bundle.TryProcess(state.StepIndex, entity, packet, ref outbound);

            if (result == PacketHandleResult.Ignored)
                return result;
            
            // Accepted
            state.StepIndex++;
            if (state.StepIndex >= bundle.StepCount)
            {
                state.BundleIndex++;
                state.StepIndex = 0;
            }
            return result;
        }
    }
}