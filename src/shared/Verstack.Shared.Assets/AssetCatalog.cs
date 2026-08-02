namespace Verstack.Shared.Assets;

public enum AssetCatalog : byte
{
    WorldGen,
    Registries,
    Tags
}

public enum AssetType : byte
{
    DimensionTypes,
    Biomes,
    Blocks,
    Items,
    Fluids
}

public enum AssetExtension : byte
{
    Nbt,
    Registry,
    Tags
}

public static class AssetCatalogPaths
{
    private const string BASE_DIRECTORY = "assets/";

    public static string GetPath(AssetCatalog catalog, AssetType asset, string name, AssetExtension extension)
    {
        string ext = GetExtensionString(extension);
        return $"{BASE_DIRECTORY}{catalog}/{asset}/{name}{ext}";
    }

    // НОВОЕ: Возвращает папку для каталога (например "assets/Tags/")
    public static string GetDirectory(AssetCatalog catalog)
    {
        return $"{BASE_DIRECTORY}{catalog}/";
    }

    // НОВОЕ: Возвращает строковое представление расширения
    public static string GetExtensionString(AssetExtension extension)
    {
        return extension switch
        {
            AssetExtension.Nbt => ".nbt",
            AssetExtension.Registry => ".registry",
            AssetExtension.Tags => ".tags",
            _ => throw new ArgumentOutOfRangeException(nameof(extension))
        };
    }
}