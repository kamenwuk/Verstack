namespace Verstack.Nbt.Assets;

public enum NbtCatalog : byte
{
    WorldGen,
    Registries,
    Tags
}

public enum NbtAsset : byte
{
    DimensionTypes,
    Biomes,
    Blocks,
    Items
}

public static class NbtAssetCatalog
{
    // Сервер запускается из bin/Debug/net10.0, поэтому ищем папку assets рядом
    private const string BASE_DIRECTORY = "assets/nbt/";

    public static string GetPath(NbtCatalog catalog, NbtAsset asset, string name)
    {
        // Формирует: assets/nbt/WorldGen/DimensionTypes/overworld.nbt
        return $"{BASE_DIRECTORY}{catalog}/{asset}/{name}.nbt";
    }
}