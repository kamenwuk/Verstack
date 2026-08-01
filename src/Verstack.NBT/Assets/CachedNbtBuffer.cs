using System.Buffers;

namespace Verstack.Nbt.Assets;

/// <summary>
/// Закэшированный буфер NBT. Держится в памяти до явной выгрузки (Unload).
/// </summary>
public sealed class CachedNbtBuffer
{
    private byte[]? _buffer;
    public ReadOnlyMemory<byte> Data { get; private set; }
    public bool IsLoaded => _buffer != null;

    public void Load(string filePath)
    {
        if (IsLoaded) return;

        int length = (int)new FileInfo(filePath).Length;
        _buffer = ArrayPool<byte>.Shared.Rent(length);

        using var fs = File.OpenRead(filePath);
        int bytesRead = fs.Read(_buffer, 0, length);

        if (bytesRead != length)
            throw new EndOfStreamException($"Не удалось прочитать весь файл {filePath}.");

        Data = _buffer.AsMemory(0, bytesRead);
    }

    public void Unload()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
            Data = ReadOnlyMemory<byte>.Empty;
        }
    }
}