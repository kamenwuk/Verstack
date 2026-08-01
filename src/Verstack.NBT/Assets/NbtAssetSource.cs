namespace Verstack.Nbt.Assets;

public static class NbtAssetSource
{
    private static readonly Dictionary<AssetKey, CachedNbtBuffer> _cachedAssets = new();

    // ───────────────  Временные (Scoped)  ───────────────

    public static ScopedNbtBuffer RentScoped(NbtCatalog catalog, NbtAsset asset, string name)
    {
        string path = NbtAssetCatalog.GetPath(catalog, asset, name);
        return ScopedNbtBuffer.Load(path);
    }

    // ───────────────  Удерживаемые (Cached)  ───────────────

    public static void PreloadCached(NbtCatalog catalog, NbtAsset asset, string name)
    {
        var key = new AssetKey(catalog, asset, name);
        if (_cachedAssets.ContainsKey(key)) return;

        var buffer = new CachedNbtBuffer();
        buffer.Load(NbtAssetCatalog.GetPath(catalog, asset, name));
        _cachedAssets.Add(key, buffer);
    }

    public static ReadOnlyMemory<byte> GetCached(NbtCatalog catalog, NbtAsset asset, string name)
    {
        var key = new AssetKey(catalog, asset, name);
        if (_cachedAssets.TryGetValue(key, out var buffer))
            return buffer.Data;

        throw new KeyNotFoundException($"Кэш NBT [{catalog}/{asset}/{name}] не был загружен.");
    }

    public static void UnloadCached(NbtCatalog catalog, NbtAsset asset, string name)
    {
        var key = new AssetKey(catalog, asset, name);
        if (_cachedAssets.TryGetValue(key, out var buffer))
        {
            buffer.Unload();
            _cachedAssets.Remove(key);
        }
    }

    private readonly struct AssetKey(NbtCatalog catalog, NbtAsset asset, string name) : IEquatable<AssetKey>
    {
        private readonly NbtCatalog _catalog = catalog;
        private readonly NbtAsset _asset = asset;
        private readonly string _name = name;

        public override int GetHashCode() => HashCode.Combine(_catalog, _asset, _name);
        public override bool Equals(object? obj) => obj is AssetKey other && _catalog == other._catalog && _asset == other._asset && _name == other._name;

        public bool Equals(AssetKey other)
        {
            return _catalog == other._catalog && _asset == other._asset && _name == other._name;
        }
    }
}