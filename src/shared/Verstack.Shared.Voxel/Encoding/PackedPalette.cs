using System.Runtime.CompilerServices;

namespace Verstack.Shared.Voxel.Encoding;

/// <summary>
/// Упакованная палитра — формат Paletted Container протокола 776: локальная палитра
/// целых id (глобальный state/biome id) + битпакинг индексов в <c>long[]</c> через <see cref="BitPacking"/>.
///
/// <para>Общий механизм для блоков и биомов; конкретику (ёмкость, BPE-пороги) задают
/// обёртки <see cref="BlockStateContainer"/> и <see cref="BiomeContainer"/>.</para>
/// </summary>
internal struct PackedPalette
{
    private readonly int _capacity;
    private readonly int _minIndirectBpe;
    private readonly int _maxIndirectBpe;

    private int[] _palette;     // _palette[index] = global id (state id или biome id)
    private int _paletteCount;
    private long[]? _data;
    private int _bitsPerEntry;

    public int BitsPerEntry => _bitsPerEntry;
    public int PaletteSize => _paletteCount;

    public PackedPalette(int capacity, int initialId, int minIndirectBpe, int maxIndirectBpe)
    {
        _capacity = capacity;
        _minIndirectBpe = minIndirectBpe;
        _maxIndirectBpe = maxIndirectBpe;
        _palette = new int[4];
        _palette[0] = initialId;
        _paletteCount = 1;
        _bitsPerEntry = 0;
        _data = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int Get(int index)
    {
        if (_bitsPerEntry == 0)
            return _palette[0];
        var paletteIndex = BitPacking.Get(_data!, index, _bitsPerEntry);
        return _palette[paletteIndex];
    }

    public void Set(int index, int id)
    {
        // Single-valued: если новое значение совпадает с текущим — ничего не делаем,
        // _data отсутствует, в битпакинг лезть нельзя (BPE=0 недопустим там).
        if (_bitsPerEntry == 0 && _palette[0] == id)
            return;

        var paletteIndex = AddOrGet(id, out var added);
        if (added)
        {
            var required = BpeForCount(_paletteCount);
            if (required != _bitsPerEntry)
                Grow(required);
        }
        BitPacking.Set(_data!, index, _bitsPerEntry, paletteIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetPaletteEntry(int paletteIndex) => _palette[paletteIndex];

    private int AddOrGet(int id, out bool added)
    {
        for (int i = 0; i < _paletteCount; i++)
        {
            if (_palette[i] == id)
            {
                added = false;
                return i;
            }
        }

        if (_paletteCount == _palette.Length)
            Array.Resize(ref _palette, _palette.Length * 2);

        _palette[_paletteCount] = id;
        added = true;
        return _paletteCount++;
    }
    
    /// <summary>
    /// Сериализовать Paletted Container в Span (wire-формат протокола 776).
    /// Порядок: BPE (UByte) → Palette → Data Array (longs в BE, длина НЕ отправляется с 1.21.5).
    /// </summary>
    public readonly int WriteTo(Span<byte> dest)
    {
        var offset = 0;
        dest[offset++] = (byte)_bitsPerEntry;

        if (_bitsPerEntry == 0)
        {
            // Single-valued: одно VarInt-значение (палитра из 1 элемента), Data Array пуст.
            offset += VarIntEncoding.Write(dest.Slice(offset), _palette[0]);
            return offset;
        }

        // Indirect: Prefixed Array of VarInt (палитра state-id).
        offset += VarIntEncoding.Write(dest.Slice(offset), _paletteCount);
        for (int i = 0; i < _paletteCount; i++)
            offset += VarIntEncoding.Write(dest.Slice(offset), _palette[i]);

        // Data Array: packed longs в big-endian. Длину НЕ пишем (клиент считает из BPE с 1.21.5).
        var longCount = BitPacking.LongCount(_capacity, _bitsPerEntry);
        for (int li = 0; li < longCount; li++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(dest.Slice(offset), _data![li]);
            offset += 8;
        }
        return offset;
    }

    private void Grow(int newBpe)
    {
        var newData = new long[BitPacking.LongCount(_capacity, newBpe)];
        if (_bitsPerEntry != 0)
        {
            // indirect → indirect: переупаковка palette-индексов.
            for (int i = 0; i < _capacity; i++)
            {
                var pi = BitPacking.Get(_data!, i, _bitsPerEntry);
                BitPacking.Set(newData, i, newBpe, pi);
            }
        }
        // single-valued → indirect: newData уже нули = palette index 0, копировать нечего.

        _data = newData;
        _bitsPerEntry = newBpe;
    }

    /// <summary>
    /// Канонический BPE для палитры данного размера.
    /// Пороги заданы обёрткой: блоки стартуют indirect с 4, биомы — с 1.
    /// </summary>
    private int BpeForCount(int count)
    {
        if (count <= 1) return 0;
        var bpe = _minIndirectBpe;
        var cap = 1 << _minIndirectBpe;
        while (cap < count && bpe < _maxIndirectBpe)
        {
            bpe++;
            cap <<= 1;
        }
        return bpe; // при count > (1<<maxIndirect) потребуется direct-формат (TODO на будущее).
    }
}