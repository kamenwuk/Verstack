using Verstack.Layers.Realm.Chunks;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Session.Chunks;

/// <summary>
/// Пул модуля чанков: флаг-компонент chunk-observer'а (<see cref="ChunkViewportInf"/>).
/// Вешается на игрока или особую сущность, грузящую чанки вокруг себя.
/// Сюда же лягут chunk-tickets/loader по мере реализации.
/// </summary>
internal sealed class ChunkCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<ChunkViewportInf> ChunkViewports = null!;
}