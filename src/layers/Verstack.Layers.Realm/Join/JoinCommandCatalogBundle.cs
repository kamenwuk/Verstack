using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Shared.Debug;
using Leopotam.EcsProto;

namespace Verstack.Layers.Realm.Join;

/// <summary>
/// Отправляет граф команд клиенту. 
/// Пока отправляем пустой граф (только Root узел), чтобы клиент не крашнулся и отключил Tab-завершение.
/// </summary>
internal sealed class JoinCommandCatalogBundle : PacketBundle
{
    public override int StepCount => 1;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        var writer = outbound.Begin();

        // Структура каждого Узла (Node) в протоколе:
        // 1. Flags (Byte): 
        //    - Биты 0x01 и 0x02 определяют тип: 0 = Root, 1 = Literal, 2 = Argument.
        //    - Бит 0x04: Is Executable (можно ли выполнить команду, закончившись на этом узле).
        //    - Бит 0x08: Has Redirect (перенаправление на другой узел).
        //    - Бит 0x10: Has Suggestions Type (есть ли кастомные подсказки для аргумента).
        // 2. Children count (VarInt): Количество дочерних узлов.
        // 3. Children (VarInt Array): Индексы дочерних узлов в массиве Nodes.
        // 4. [Опционально] Redirect node index (VarInt): Если установлен бит 0x08.
        // 5. [Опционально] Name (String): Если тип Literal (1) или Argument (2).
        // 6. [Опционально] Parser ID (VarInt): Если тип Argument (2).
        // 7. [Опционально] Suggestions Type (Identifier): Если установлен бит 0x10.

        writer.WriteVarInt(0x10) // Clientbound commands, Play ID: 16 (0x10) — ID пакета команд.
            .WriteVarInt(1)      // Nodes length (Prefixed Array) — Количество узлов в графе (1 узел — Root).
            .WriteByte(0x00)  // Node 0 Flags (Byte) — Тип 0 (Root), не исполняемый, без редиректов и подсказок.
            .WriteVarInt(0)      // Node 0 Children count (VarInt) — У корня 0 дочерних узлов (нет команд).
            .WriteVarInt(0);     // Root index (VarInt) — Индекс корневого узла в массиве (0, так как это первый и единственный узел).

        outbound.Commit(ref writer);

        Logger.Debug(LogKey.PacketPlayCommands, (int)entity);

        return PacketHandleResult.Accepted;
    }
}