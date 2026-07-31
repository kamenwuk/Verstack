// using System.Text;
// using Verstack.Layer123.Global;
// using Verstack.Network.DataTypes;
// using Verstack.Network.Packet;
//
// namespace Verstack.Layer123.Gateway.Tests;
//
// /// <summary>
// /// Тесты данных реестров для фазы Configuration (Minecraft 26.2):
// /// <see cref="SyncedRegistryCatalog"/> (идентификаторы 29 синхронизируемых реестров),
// /// <see cref="MandatoryRegistryEntries"/> (обязательные записи 13 реестров) и
// /// <see cref="RegistryTagCatalog"/> (ванильные теги 20 реестров для Update Tags).
// /// Проверяют порядок, размеры, выравнивание индексов и wire-формат пакетов Registry Data (0x07) и Update Tags (0x08).
// /// </summary>
// public class RegistryConfigurationDataTests
// {
//     // ------------------- SyncedRegistryCatalog -------------------
//
//     [Fact]
//     public void SyncedRegistryIds_HasExactly29Registries()
//     {
//         Assert.Equal(29, SyncedRegistryCatalog.EntryIds.Length);
//     }
//
//     [Fact]
//     public void EntryIds_IndexAlignedWithSyncedRegistryIds()
//     {
//         Assert.Equal(SyncedRegistryCatalog.EntryIds.Length,
//             MandatoryRegistryEntries.EntryIds.Length);
//     }
//
//     [Fact]
//     public void FirstRegistry_IsWorldgenBiome_WithPrefix()
//     {
//         byte[] first = SyncedRegistryCatalog.EntryIds[0];
//         Assert.Equal("minecraft:worldgen/biome"u8.ToArray(), first);
//     }
//
//     [Fact]
//     public void LastRegistry_IsTimeline()
//     {
//         byte[] last = SyncedRegistryCatalog.EntryIds[^1];
//         Assert.Equal("minecraft:timeline"u8.ToArray(), last);
//     }
//
//     [Fact]
//     public void AllRegistryIds_StartWithMinecraftNamespace()
//     {
//         foreach (byte[] id in SyncedRegistryCatalog.EntryIds)
//             Assert.StartsWith("minecraft:", Encoding.UTF8.GetString(id));
//     }
//
//     [Fact]
//     public void GetIdBytes_ReturnsCorrectArray()
//     {
//         byte[] biome = SyncedRegistryCatalog.GetIdBytes(SyncedRegistryCatalog.RegistryType.Biome);
//         Assert.Equal("minecraft:worldgen/biome"u8.ToArray(), biome);
//
//         byte[] timeline = SyncedRegistryCatalog.GetIdBytes(SyncedRegistryCatalog.RegistryType.Timeline);
//         Assert.Equal("minecraft:timeline"u8.ToArray(), timeline);
//     }
//
//     [Fact]
//     public void RegistryTypeEnum_MatchesIdBytesIndexes()
//     {
//         Assert.Equal("minecraft:chat_type"u8.ToArray(),
//             SyncedRegistryCatalog.EntryIds[(int)SyncedRegistryCatalog.RegistryType.ChatType]);
//         Assert.Equal("minecraft:painting_variant"u8.ToArray(),
//             SyncedRegistryCatalog.EntryIds[(int)SyncedRegistryCatalog.RegistryType.PaintingVariant]);
//     }
//
//     // ------------------- MandatoryRegistryEntries -------------------
//
//     [Fact]
//     public void Exactly13RegistriesHaveEntries_OthersEmpty()
//     {
//         int withEntries = 0;
//         foreach (byte[][] entries in MandatoryRegistryEntries.EntryIds)
//             if (entries.Length > 0)
//                 withEntries++;
//
//         Assert.Equal(13, withEntries);
//     }
//
//     [Fact]
//     public void PaintingVariant_Has51Entries()
//     {
//         byte[][] painting = MandatoryRegistryEntries.EntryIds[16];
//         Assert.Equal(51, painting.Length);
//         Assert.Equal("minecraft:alban"u8.ToArray(), painting[0]);
//         Assert.Equal("minecraft:wither"u8.ToArray(), painting[^1]);
//     }
//
//     [Fact]
//     public void GetEntries_ReturnsCorrectEntries()
//     {
//         byte[][] wolfVariants = MandatoryRegistryEntries.GetEntries(MandatoryRegistryEntries.Registry.WolfVariant);
//         Assert.Equal(9, wolfVariants.Length);
//         Assert.Equal("minecraft:ashen"u8.ToArray(), wolfVariants[0]);
//
//         byte[][] empty = MandatoryRegistryEntries.GetEntries(MandatoryRegistryEntries.Registry.Biome);
//         Assert.Empty(empty);
//     }
//
//     [Fact]
//     public void MandatoryEntries_EnumMatchesIndexes()
//     {
//         Assert.Equal(0, (int)MandatoryRegistryEntries.Registry.Biome);
//         Assert.Equal(28, (int)MandatoryRegistryEntries.Registry.Timeline);
//         Assert.Equal(16, (int)MandatoryRegistryEntries.Registry.PaintingVariant);
//     }
//
//     // ------------------- VanillaRegistryTags -------------------
//
//     [Fact]
//     public void VanillaRegistryTags_HasExactly20Registries()
//     {
//         Assert.Equal(20, RegistryTagCatalog.RegistryNames.Length);
//         Assert.Equal(20, RegistryTagCatalog.TagNames.Length);
//     }
//
//     [Fact]
//     public void TagRegistryNames_StartWithMinecraftPrefix()
//     {
//         foreach (byte[] name in RegistryTagCatalog.RegistryNames)
//             Assert.StartsWith("minecraft:", Encoding.UTF8.GetString(name));
//     }
//
//     [Fact]
//     public void FirstTagRegistry_IsBlock()
//     {
//         Assert.Equal("minecraft:block"u8.ToArray(), RegistryTagCatalog.RegistryNames[0]);
//     }
//
//     [Fact]
//     public void LastTagRegistry_IsFlatLevelGeneratorPreset()
//     {
//         Assert.Equal("minecraft:worldgen/flat_level_generator_preset"u8.ToArray(),
//             RegistryTagCatalog.RegistryNames[^1]);
//     }
//
//     [Theory]
//     [InlineData(0, 265)]  // block
//     [InlineData(1, 224)]  // item
//     [InlineData(3, 68)]   // worldgen/biome
//     [InlineData(12, 3)]   // instrument
//     [InlineData(16, 1)]   // painting_variant
//     public void TagCount_MatchesExpected(int index, int expectedCount)
//     {
//         byte[][] tags = RegistryTagCatalog.TagNames[index];
//         Assert.Equal(expectedCount, tags.Length);
//     }
//
//     [Fact]
//     public void GetTags_ReturnsCorrectTags()
//     {
//         byte[][] blockTags = RegistryTagCatalog.GetTags(RegistryTagCatalog.Registry.Block);
//         Assert.Equal(265, blockTags.Length);
//         Assert.Equal("minecraft:acacia_logs"u8.ToArray(), blockTags[0]);
//         Assert.Equal("minecraft:wool_carpets"u8.ToArray(), blockTags[^1]);
//     }
//
//     [Fact]
//     public void AllTagNames_HaveMinecraftNamespace()
//     {
//         foreach (byte[][] tagList in RegistryTagCatalog.TagNames)
//         foreach (byte[] tag in tagList)
//             Assert.StartsWith("minecraft:", Encoding.UTF8.GetString(tag));
//     }
//
//     // ------------------- Wire-format tests -------------------
//
//     [Fact]
//     public void RegistryData_ListingOnly_WritesExpectedBytes()
//     {
//         Span<byte> buffer = stackalloc byte[64];
//         var w = new SpanWriter(buffer);
//
//         VarInt.Write(ref w, 0x07);
//         Utf8String.Write(ref w, "minecraft:chat_type"u8.ToArray());
//         VarInt.Write(ref w, 0);
//
//         Assert.Equal(ParseHex("07 13 6D 69 6E 65 63 72 61 66 74 3A 63 68 61 74 5F 74 79 70 65 00"),
//             w.WrittenSpan.ToArray());
//     }
//
//     [Fact]
//     public void RegistryData_OneEntryWithEmptyTag_WritesExpectedBytes()
//     {
//         Span<byte> buffer = stackalloc byte[64];
//         var w = new SpanWriter(buffer);
//
//         VarInt.Write(ref w, 0x07);
//         Utf8String.Write(ref w, "minecraft:cat_sound_variant"u8.ToArray());
//         VarInt.Write(ref w, 1);
//         Utf8String.Write(ref w, "minecraft:classic"u8.ToArray());
//         w.GetSpan(1)[0] = 0;
//         w.Advance(1);
//
//         Assert.Equal(ParseHex("07 1B 6D 69 6E 65 63 72 61 66 74 3A 63 61 74 5F 73 6F 75 6E 64 5F 76 61 72 69 61 6E 74 "
//             + "01 11 6D 69 6E 65 63 72 61 66 74 3A 63 6C 61 73 73 69 63 00"),
//             w.WrittenSpan.ToArray());
//     }
//
//     [Fact]
//     public void UpdateTags_WireFormat_EmptyTagList()
//     {
//         Span<byte> buffer = stackalloc byte[64];
//         var w = new SpanWriter(buffer);
//
//         VarInt.Write(ref w, 0x08);
//         VarInt.Write(ref w, 1);
//         Utf8String.Write(ref w, "minecraft:block"u8.ToArray());
//         VarInt.Write(ref w, 0);
//
//         // Длина "minecraft:block" = 15 байт (0x0F)
//         Assert.Equal(ParseHex("08 01 0F 6D 69 6E 65 63 72 61 66 74 3A 62 6C 6F 63 6B 00"),
//             w.WrittenSpan.ToArray());
//     }
//
//     private static byte[] ParseHex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));
// }