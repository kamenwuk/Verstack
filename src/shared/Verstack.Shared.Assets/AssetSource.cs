using System.Text;

namespace Verstack.Shared.Assets;

public static class AssetSource
{
    private static readonly Dictionary<AssetKey, CachedAssetBuffer> CachedAssets = new();
    private static TagBatchEntry[] _tagBatch = Array.Empty<TagBatchEntry>();
    
    public readonly struct TagBatchEntry(ReadOnlyMemory<byte> registryId, ReadOnlyMemory<byte> data)
    {
        public readonly ReadOnlyMemory<byte> RegistryId = registryId;
        public readonly ReadOnlyMemory<byte> Data = data;
    }
    
    /// <summary>
    /// Вызывать ОДИН РАЗ при старте сервера. Загружает все .tags файлы в память.
    /// </summary>
    public static void PreloadTagBatch()
    {
        string tagsDir = AssetCatalogPaths.GetDirectory(AssetCatalog.Tags);
        if (!Directory.Exists(tagsDir)) return;

        string[] tagFiles = Directory.GetFiles(tagsDir, "*.tags", SearchOption.AllDirectories);
        string baseDir = Path.GetFullPath(tagsDir);
    
        var list = new List<TagBatchEntry>(tagFiles.Length);

        foreach (string filePath in tagFiles)
        {
            string relativePath = Path.GetRelativePath(baseDir, filePath);
            relativePath = relativePath.Substring(0, relativePath.Length - AssetCatalogPaths.GetExtensionString(AssetExtension.Tags).Length);
        
            // НОВОЕ: Никаких Replace('_', '/')! Просто меняем слеш Windows на слеш URL.
            string registryIdStr = ("minecraft:" + relativePath.Replace('\\', '/')).ToLower();
        
            ReadOnlyMemory<byte> registryIdBytes = Encoding.UTF8.GetBytes(registryIdStr);
        
            var buffer = new CachedAssetBuffer();
            buffer.Load(filePath);
        
            list.Add(new TagBatchEntry(registryIdBytes, buffer.Data));
        }

        _tagBatch = list.ToArray();
    }
    
    /// <summary>
    /// Возвращает загруженную пачку тегов для отправки в пакете Update Tags.
    /// </summary>
    public static TagBatchEntry[] GetTagBatch() => _tagBatch;

    
    // ───────────────  Временные (Scoped)  ───────────────

    public static ScopedAssetBuffer RentScoped(AssetCatalog catalog, AssetType asset, string name, AssetExtension extension)
    {
        string path = AssetCatalogPaths.GetPath(catalog, asset, name, extension);
        return ScopedAssetBuffer.Load(path);
    }

    // ───────────────  Удерживаемые (Cached)  ───────────────

    public static void PreloadCached(AssetCatalog catalog, AssetType asset, string name, AssetExtension extension)
    {
        var key = new AssetKey(catalog, asset, name);
        if (CachedAssets.ContainsKey(key)) return;

        var buffer = new CachedAssetBuffer();
        string path = AssetCatalogPaths.GetPath(catalog, asset, name, extension);
        buffer.Load(path);
        CachedAssets.Add(key, buffer);
    }

    public static ReadOnlyMemory<byte> GetCached(AssetCatalog catalog, AssetType asset, string name)
    {
        var key = new AssetKey(catalog, asset, name);
        if (CachedAssets.TryGetValue(key, out var buffer))
            return buffer.Data;

        throw new KeyNotFoundException($"Кэш актива [{catalog}/{asset}/{name}] не был загружен.");
    }

    public static void UnloadCached(AssetCatalog catalog, AssetType asset, string name)
    {
        var key = new AssetKey(catalog, asset, name);
        if (CachedAssets.TryGetValue(key, out var buffer))
        {
            buffer.Unload();
            CachedAssets.Remove(key);
        }
    }

    private readonly struct AssetKey(AssetCatalog catalog, AssetType asset, string name) : IEquatable<AssetKey>
    {
        private readonly AssetCatalog _catalog = catalog;
        private readonly AssetType _asset = asset;
        private readonly string _name = name;

        public override int GetHashCode() => HashCode.Combine(_catalog, _asset, _name);
        public override bool Equals(object? obj) => obj is AssetKey other && _catalog == other._catalog && _asset == other._asset && _name == other._name;

        public bool Equals(AssetKey other)
        {
            return _catalog == other._catalog && _asset == other._asset && _name == other._name;
        }
    }
}