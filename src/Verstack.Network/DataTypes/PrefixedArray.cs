using System.Runtime.CompilerServices;
using System.Buffers;

namespace Verstack.Network.DataTypes;

/// <summary>
/// Записывает один элемент массива в буфер. Аналог статических Write-методов других DataType'ов
/// (например <see cref="Numeric.WriteInt"/>), обёрнутый в делегат для универсальной записи массива.
/// </summary>
public delegate void WriteArrayItem<T>(IBufferWriter<byte> writer, in T item);

/// <summary>
/// Читает один элемент массива из потока. Бросает <see cref="EndOfStreamException"/> при нехватке данных —
/// как и другие DataType-ридеры.
/// </summary>
public delegate T ReadArrayItem<out T>(ref SequenceReader<byte> reader);

/// <summary>
/// Prefixed Array протокола Minecraft: <c>VarInt</c>-длина + N элементов подряд. Используется для
/// properties в Game Profile, PublicKey/Verify Token в Encryption и т.п.
///
/// Generic + делегаты — это cold path (Login/Configuration, раз на соединение): аллокация массива-результата
/// и вызовы делегатов здесь допустимы. В hot path (Play-чанки) массивы кодируются специализированно.
/// </summary>
public static class PrefixedArray
{
    /// <summary>
    /// Пишет массив: VarInt-длину, затем каждый элемент через <paramref name="writeItem"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(IBufferWriter<byte> writer, ReadOnlySpan<T> items, WriteArrayItem<T> writeItem)
    {
        VarInt.Write(writer, items.Length);
        for (var i = 0; i < items.Length; i++)
            writeItem(writer, in items[i]);
    }

    /// <summary>
    /// Читает массив: VarInt-длину, затем N элементов через <paramref name="readItem"/>.
    /// Длина &lt; 0 трактуется как ошибка формата.
    /// </summary>
    public static T[] Read<T>(ref SequenceReader<byte> reader, ReadArrayItem<T> readItem)
    {
        int length = VarInt.Read(ref reader);
        if (length < 0)
            throw new InvalidDataException($"Недопустимая длина массива: {length}");

        var items = new T[length];
        for (var i = 0; i < length; i++)
            items[i] = readItem(ref reader);
        return items;
    }
}