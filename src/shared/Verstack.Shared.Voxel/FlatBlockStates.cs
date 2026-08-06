namespace Verstack.Shared.Voxel;

/// <summary>
/// Захардкоженные state-id базовых блоков для плоского мира (MC 26.2, протокол 776).
///
/// Значения проверены по reports/blocks.json (Minecraft Data Generator)
/// и стабильны для версий 1.21.4–1.21.8:
///   https://github.com/PrismarineJS/minecraft-data/blob/master/data/pc/1.21.8/blocks.json
///
/// Внимание: это STATE id (для чанк-палитры), НЕ block id из block.registry.
/// Block id и state id расходятся для блоков со свойствами (см. grass_block).
///
/// Когда появится полный реестр block-states из DataCompiler — этот класс
/// заменяется на runtime-ридер; значения совпадут.
/// </summary>
public static class FlatBlockStates
{
    /// <summary>Воздух. Без свойств.</summary>
    public const int AIR = 0;

    /// <summary>Камень. Без свойств.</summary>
    public const int STONE = 1;

    /// <summary>Трава, snowy=false (зелёная).
    /// НЕ 9 — это snowy=true (заснеженная трава).</summary>
    public const int GRASS_BLOCK = 9;

    /// <summary>Земля. Без свойств.</summary>
    public const int DIRT = 10;

    /// <summary>Бедрок. Без свойств.</summary>
    public const int BEDROCK = 85;

    /// <summary>Биом plains — runtime id 0 в синхронизированном реестре worldgen/biome
    /// (см. SyncedRegistryCatalog: plains — первая mandatory-запись).
    /// НЕ 40 (индекс в полном списке биомов Prismarine).</summary>
    public const int BIOME_PLAINS = 0;

    public static readonly BlockState Air = new(AIR);
    public static readonly BlockState Stone = new(STONE);
    public static readonly BlockState GrassBlock = new(GRASS_BLOCK);
    public static readonly BlockState Dirt = new(DIRT);
    public static readonly BlockState Bedrock = new(BEDROCK);
}