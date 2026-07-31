using System.Runtime.CompilerServices;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Verstack.Network.DataTypes;

/// <summary>
/// UUID протокола Minecraft: 128 бит, big-endian (RFC 4122). Представление — <see cref="Guid"/>.
///
/// Wire-формат — ровно 16 байт в сетевом (big-endian) порядке. BCL <see cref="Guid"/> хранит те же 128 бит,
/// но <see cref="Guid.ToByteArray"/> отдаёт их в смешанном (little-endian для первых трёх полей) порядке —
/// поэтому используем перегрузки с <c>bigEndian: true</c> (доступны с .NET 9): они соответствуют RFC напрямую.
/// </summary>
public static class Uuid
{
    public const int SIZE = 16;

    /// <summary>
    /// Читает UUID как 16 big-endian байт. Бросает <see cref="EndOfStreamException"/>, если в потоке меньше 16 байт.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid Read(ref SequenceReader<byte> reader)
    {
        if (!reader.TryReadExact(SIZE, out ReadOnlySequence<byte> bytes))
            throw new EndOfStreamException("Не удалось прочитать UUID: достигнут конец потока.");

        // IsSingleSegment — типичный случай (RawPacket.Data — один массив). Ветвление без аллокаций.
        if (bytes.IsSingleSegment)
            return new Guid(bytes.FirstSpan, bigEndian: true);

        Span<byte> buffer = stackalloc byte[SIZE];
        bytes.CopyTo(buffer);
        return new Guid(buffer, bigEndian: true);
    }

    /// <summary>
    /// Пишет UUID как 16 big-endian байт прямо в буфер writer'а, без промежуточной аллокации.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(IBufferWriter<byte> writer, Guid value)
    {
        value.TryWriteBytes(writer.GetSpan(SIZE), bigEndian: true, out _);
        writer.Advance(SIZE);
    }
    
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static void Write(ref Packet.SpanWriter writer, Guid value)
    // {
    //     value.TryWriteBytes(writer.GetSpan(SIZE), bigEndian: true, out _);
    //     writer.Advance(SIZE);
    // }

    /// <summary>
    /// Генерирует offline-UUID (версия 3) для имени игрока. Повторяет семантику
    /// <c>java.util.UUID.nameUUIDFromBytes</c> ванильного сервера: MD5 от UTF-8 байтов строки
    /// <c>"OfflinePlayer:" + name</c>, с выставлением version=3 и variant RFC 4122.
    ///
    /// Префикс <c>"OfflinePlayer:"</c> — код самого ванильного сервера (<c>ServerLoginPacketListenerImpl</c>),
    /// не конвенция плагинов. Он даёт детерминированный, воспроизводимый UUID для одного имени —
    /// ключ к данным игрока, единый с другими offline-серверами и перезапусками.
    /// </summary>
    public static Guid GenerateOfflinePlayer(string name)
    {
        // Префикс — чистый ASCII ("OfflinePlayer:" = 14 байт), имя — UTF-8 (до 16 символов по протоколу).
        // Cold path: раз на соединение при логине, простая аллокация честнее ручного stackalloc-трюка.
        const string PREFIX = "OfflinePlayer:";
        byte[] input = new byte[PREFIX.Length + Encoding.UTF8.GetByteCount(name)];
        Encoding.ASCII.GetBytes(PREFIX, 0, PREFIX.Length, input, 0);
        Encoding.UTF8.GetBytes(name, 0, name.Length, input, PREFIX.Length);

        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(input, hash);

        // version = 3 (name-based, MD5): верхние 4 бита 7-го байта (index 6) → 0011.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        // variant = RFC 4122: верхние 2 бита 9-го байта (index 8) → 10.
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash, bigEndian: true);
    }
}