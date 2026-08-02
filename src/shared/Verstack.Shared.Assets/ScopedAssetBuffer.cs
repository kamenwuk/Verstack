using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace Verstack.Shared.Assets;

/// <summary>
/// Временный буфер актива. Читает бинарный файл напрямую в буфер из ArrayPool.
/// Живет только в области видимости (в блоке using).
/// </summary>
public ref struct ScopedAssetBuffer
{
    private byte[]? _rentedBuffer;
    public ReadOnlySpan<byte> Data { get; }

    private ScopedAssetBuffer(byte[] buffer, int length)
    {
        _rentedBuffer = buffer;
        Data = buffer.AsSpan(0, length);
    }

    public static ScopedAssetBuffer Load(string filePath)
    {
        // 1. Открываем дескриптор файла напрямую (дешево, без создания FileStream)
        SafeFileHandle handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.None);
        
        try
        {
            // 2. Узнаем длину файла через дескриптор
            long length = RandomAccess.GetLength(handle);
            int len = (int)length;

            // 3. Берем буфер из пула
            byte[] buffer = ArrayPool<byte>.Shared.Rent(len);

            // 4. Читаем напрямую в буфер (минуя слои FileStream)
            int bytesRead = RandomAccess.Read(handle, buffer.AsSpan(0, len), 0);

            if (bytesRead != len)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw new EndOfStreamException($"Не удалось прочитать весь файл {filePath}.");
            }

            return new ScopedAssetBuffer(buffer, bytesRead);
        }
        finally
        {
            // 5. Мгновенно освобождаем дескриптор
            handle.Dispose();
        }
    }

    public void Dispose()
    {
        if (_rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
            _rentedBuffer = null;
        }
    }
}