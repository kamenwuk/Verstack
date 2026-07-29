using Verstack.Layer.Global;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;

namespace Verstack.Layer.Gateway.Tests;

/// <summary>
/// Тесты данных synced-реестров Minecraft 26.2: canonical-список из 29 resource-location
/// (добыт из bytecode <c>RegistryDataLoader.SYNCHRONIZED_REGISTRIES</c>) + entry-ids для 13
/// обязательных реестров (из bundled-datapack клиент-jar). Контролируют порядок, границы и
/// wire-формат listing-payload (Registry Data, S→C 0x07).
///
/// Разбор: <c>.zcode/research/vanilla-26.2-synced-registries.md</c>,
/// <c>.zcode/research/mandatory-registry-entries-26.2.md</c>,
/// <c>.zcode/research/registry-data-packet-wire-format.md</c>.
/// </summary>
public class VanillaRegistriesTests
{
    /// <summary>
    /// Synced-список 26.2 содержит ровно 29 реестров. Это инвариант версии — любое отклонение
    /// = рассинхрон с клиентом 26.2 (он валидирует точный набор synced-реестров).
    /// </summary>
    [Fact]
    public void SyncedRegistryIds_HasExactly29Registries()
    {
        Assert.Equal(29, VanillaSyncedRegistries.SyncedRegistryIds.Length);
    }

    /// <summary>
    /// EntryIds index-aligned с SyncedRegistryIds: та же длина 29. Иначе lookup по индексу в
    /// <c>KnownPacksBundle</c> выйдет за границы.
    /// </summary>
    [Fact]
    public void EntryIds_IndexAlignedWithSyncedRegistryIds()
    {
        Assert.Equal(VanillaSyncedRegistries.SyncedRegistryIds.Length,
            VanillaRegistryEntries.EntryIds.Length);
    }

    /// <summary>
    /// BIOME → minecraft:worldgen/biome — единственный с префиксом worldgen/. Контринтуитивное:
    /// Java-поле <c>Registries.BIOME</c> разворачивается через <c>createRegistryKey("worldgen/biome")</c>,
    /// а не в <c>minecraft:biome</c>. Главный «грабль» списка.
    /// </summary>
    [Fact]
    public void FirstRegistry_IsWorldgenBiome_WithPrefix()
    {
        byte[] first = VanillaSyncedRegistries.SyncedRegistryIds[0];
        Assert.Equal("minecraft:worldgen/biome"u8.ToArray(), first);
    }

    /// <summary>
    /// Последний synced-реестр — minecraft:timeline. Контроль нижней границы порядка (canonical
    /// порядок из bytecode <c>SYNCHRONIZED_REGISTRIES</c>).
    /// </summary>
    [Fact]
    public void LastRegistry_IsTimeline()
    {
        byte[] last = VanillaSyncedRegistries.SyncedRegistryIds[^1];
        Assert.Equal("minecraft:timeline"u8.ToArray(), last);
    }

    /// <summary>
    /// 13 обязательных реестров имеют ≥1 entry; 16 остальных — пустые (count=0 в listing).
    /// Клиент 26.2 валидирует non-empty для variant-реестров + painting_variant.
    /// </summary>
    [Fact]
    public void Exactly13RegistriesHaveEntries_OthersEmpty()
    {
        int withEntries = 0;
        foreach (byte[][] entries in VanillaRegistryEntries.EntryIds)
            if (entries.Length > 0)
                withEntries++;

        Assert.Equal(13, withEntries);
    }

    /// <summary>
    /// painting_variant = 51 entry (самый большой обязательный реестр). Контроль canonical-набора
    /// картин ваниллы.
    /// </summary>
    [Fact]
    public void PaintingVariant_Has51Entries()
    {
        // Индекс 16 = painting_variant (см. VanillaRegistryEntries inline-комментарии).
        byte[][] painting = VanillaRegistryEntries.EntryIds[16];
        Assert.Equal(51, painting.Length);
        // Первый alphabetical = alban, последний = wither.
        Assert.Equal("minecraft:alban"u8.ToArray(), painting[0]);
        Assert.Equal("minecraft:wither"u8.ToArray(), painting[^1]);
    }

    /// <summary>
    /// Wire-формат listing-payload для listing-only реестра (count=0):
    /// <c>[0x07][Identifier registry][0x00]</c>. Эталон свёрен вручную по
    /// <c>.zcode/research/registry-data-packet-wire-format.md</c>.
    /// chat_type = 19 UTF-8 байт → VarInt-length = 0x13.
    /// Защита от регресса wire-формата (framed stream-codec 26.2, без корневого Compound).
    /// </summary>
    [Fact]
    public void RegistryData_ListingOnly_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[64];
        var w = new SpanWriter(buffer);

        VarInt.Write(ref w, 0x07);                                  // Clientbound Registry Data ID
        Utf8String.Write(ref w, "minecraft:chat_type"u8.ToArray()); // Identifier (реестр)
        VarInt.Write(ref w, 0);                                     // entries count = 0 (listing-only)

        Assert.Equal(ParseHex("07 13 6D 69 6E 65 63 72 61 66 74 3A 63 68 61 74 5F 74 79 70 65 00"),
            w.WrittenSpan.ToArray());
    }

    /// <summary>
    /// Wire-формат payload для обязательного реестра с одним entry (id + TAG_End):
    /// <c>[0x07][Identifier registry][0x01][Identifier entry][0x00]</c>.
    /// 0x00 после entry = Optional&lt;Tag&gt; empty (TAG_End) — клиент берёт тело из bundled-datapack.
    /// cat_sound_variant = 27 байт → 0x1B; classic = 17 байт → 0x11.
    /// </summary>
    [Fact]
    public void RegistryData_OneEntryWithEmptyTag_WritesExpectedBytes()
    {
        Span<byte> buffer = stackalloc byte[64];
        var w = new SpanWriter(buffer);

        VarInt.Write(ref w, 0x07);
        Utf8String.Write(ref w, "minecraft:cat_sound_variant"u8.ToArray()); // Identifier (реестр)
        VarInt.Write(ref w, 1);                                  // 1 entry
        Utf8String.Write(ref w, "minecraft:classic"u8.ToArray()); // Identifier (entry)
        w.GetSpan(1)[0] = 0;                                     // Optional<Tag> = empty → TAG_End
        w.Advance(1);

        Assert.Equal(ParseHex("07 1B 6D 69 6E 65 63 72 61 66 74 3A 63 61 74 5F 73 6F 75 6E 64 5F 76 61 72 69 61 6E 74 "
            + "01 11 6D 69 6E 65 63 72 61 66 74 3A 63 6C 61 73 73 69 63 00"),
            w.WrittenSpan.ToArray());
    }

    /// <summary>
    /// Парсинг hex-строки (с пробелами) в byte[]. Образец из NBT-тестов
    /// (<c>ModifiedUtf8Tests.ParseHex</c>) — единый стиль парсинга эталонов в проекте.
    /// </summary>
    private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
}