using Verstack.Network.Packet.Writers;
using Verstack.Network.Packet;
using Verstack.Layer.Global;
using Verstack.Nbt.Assets;
using Leopotam.EcsProto;
using Verstack.Debug;
using Verstack.Nbt;

namespace Verstack.Layer.Gateway.Bundles;

internal sealed class KnownPacksBundle : PacketBundle
{
    public override int StepCount => 3;

    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
    {
        switch (stepIndex)
        {
            case 0:
            {
                // 0x02 - Plugin Message (serverbound) - пропускаем (brand)
                if (packet.Id == 0x02) return PacketHandleResult.Ignored;
                // 0x07 - Known Packs (serverbound) - ждем
                if (packet.Id != 0x07) return PacketHandleResult.Kick;

                // --- 1. S→C Registry Data (ID 0x07) ---
                byte[][] syncedIds = SyncedRegistryCatalog.RegistryIds;
                byte[][][] entryIds = SyncedRegistryCatalog.MandatoryEntries;

                if (syncedIds.Length != entryIds.Length)
                    throw new InvalidOperationException("Arrays length mismatch");

                // Увеличенные буферы для NBT (было 16/2048, стало с запасом)
                Span<NbtFrame> nbtFrames = stackalloc NbtFrame[32];
                Span<byte> nbtBuffer = stackalloc byte[8192];

                for (int i = 0; i < syncedIds.Length; i++)
                {
                    byte[] registryId = syncedIds[i];
                    byte[][] entries = entryIds[i];

                    // Получаем writer из outbound
                    var registryData = outbound.Begin();
                    
                    registryData.WriteVarInt(0x07) // registry_data
                                .WriteString(registryId)
                                .WriteVarInt(entries.Length);

                    foreach (byte[] entryName in entries)
                    {
                        registryData.WriteString(entryName);

                        bool isDimensionType = i == (int)SyncedRegistryCatalog.RegistryType.DimensionType;
                        bool isOverworld = isDimensionType && entryName.SequenceEqual("minecraft:overworld"u8);

                        if (isOverworld)
                        {
                            // 2. Prefixed Optional NBT: Boolean = true (0x01)
                            registryData.WriteBool(true);

                            // // 3. NBT Data – точная копия дефолтного overworld
                            // var nbtWriter = new NbtWriter(nbtBuffer, nbtFrames, networked: true);
                            // nbtWriter.BeginRootCompound()
                            //     .WriteFloat("ambient_light"u8, 0.0f)
                            //
                            //     // --- attributes ---
                            //     .BeginCompound("attributes"u8)
                            //         // Ambient sounds
                            //         .BeginCompound("minecraft:audio/ambient_sounds"u8)
                            //             .BeginCompound("mood"u8)
                            //                 .WriteInt("block_search_extent"u8, 8)
                            //                 .WriteInt("offset"u8, 2)
                            //                 .WriteString("sound"u8, "minecraft:ambient.cave"u8)
                            //                 .WriteInt("tick_delay"u8, 6000)
                            //             .EndCompound()
                            //         .EndCompound()
                            //
                            //         // Background music
                            //         .BeginCompound("minecraft:audio/background_music"u8)
                            //             .BeginCompound("creative"u8)
                            //                 .WriteInt("max_delay"u8, 24000)
                            //                 .WriteInt("min_delay"u8, 12000)
                            //                 .WriteString("sound"u8, "minecraft:music.creative"u8)
                            //             .EndCompound()
                            //             .BeginCompound("default"u8)
                            //                 .WriteInt("max_delay"u8, 24000)
                            //                 .WriteInt("min_delay"u8, 12000)
                            //                 .WriteString("sound"u8, "minecraft:music.game"u8)
                            //             .EndCompound()
                            //         .EndCompound()
                            //
                            //         // Bed rule
                            //         .BeginCompound("minecraft:gameplay/bed_rule"u8)
                            //             .WriteString("can_set_spawn"u8, "always"u8)
                            //             .WriteString("can_sleep"u8, "when_dark"u8)
                            //             .BeginCompound("error_message"u8)
                            //                 .WriteString("translate"u8, "block.minecraft.bed.no_sleep"u8)
                            //             .EndCompound()
                            //         .EndCompound()
                            //
                            //         // Gameplay flags
                            //         .WriteBool("minecraft:gameplay/nether_portal_spawns_piglin"u8, true)
                            //         .WriteBool("minecraft:gameplay/respawn_anchor_works"u8, false)
                            //
                            //         // Visual colors – теперь целые числа!
                            //         .WriteInt("minecraft:visual/ambient_light_color"u8, unchecked((int)0xFF0A0A0A))
                            //         .WriteInt("minecraft:visual/cloud_color"u8,           unchecked((int)0xCCFFFFFF))
                            //         .WriteFloat("minecraft:visual/cloud_height"u8, 192.33f)
                            //         .WriteInt("minecraft:visual/fog_color"u8,              unchecked((int)0xFFC0D8FF))
                            //         .WriteInt("minecraft:visual/sky_color"u8,              unchecked((int)0xFF78A7FF))
                            //     .EndCompound()
                            //
                            //     // --- Остальные поля корневого уровня ---
                            //     .WriteDouble("coordinate_scale"u8, 1.0)
                            //     .WriteString("default_clock"u8, "minecraft:overworld"u8)
                            //     .WriteBool("has_ceiling"u8, false)          // ← исправлено: в overworld должно быть false
                            //     .WriteBool("has_ender_dragon_fight"u8, false)
                            //     .WriteBool("has_skylight"u8, true)
                            //     .WriteInt("height"u8, 384)
                            //     .WriteString("infiniburn"u8, "#minecraft:infiniburn_overworld"u8)
                            //     .WriteInt("logical_height"u8, 384)
                            //     .WriteInt("min_y"u8, -64)
                            //     .WriteInt("monster_spawn_block_light_limit"u8, 0)
                            //     .BeginCompound("monster_spawn_light_level"u8)
                            //         .WriteString("type"u8, "minecraft:uniform"u8)
                            //         .WriteInt("min_inclusive"u8, 0)
                            //         .WriteInt("max_inclusive"u8, 7)
                            //     .EndCompound()
                            //     .WriteString("timelines"u8, "#minecraft:in_overworld"u8)
                            // .EndCompound();
                            //
                            // ReadOnlySpan<byte> nbtData = nbtWriter.Finish();
                            using var nbtData = NbtAssetSource.RentScoped(
                                NbtCatalog.WorldGen, 
                                NbtAsset.DimensionTypes, 
                                "overworld" // Убедись, что файл называется overworld.nbt
                            );
                            // Раньше тут было GetSpan + CopyTo + Advance
                            registryData.WriteSpanRaw(nbtData.Data);
                        }
                        else
                        {
                            // Нет данных для других записей
                            registryData.WriteBool(false);
                        }
                    }
                    
                    // Коммитим накопленный payload во framing-буфер
                    outbound.Commit(ref registryData);
                }

                return PacketHandleResult.Continue;
            }
            case 1:
            {
                // --- 2. S→C Update Tags (ID 0x0D) ---
                // Полностью переделано: отправляем все теги для каждого реестра одним пакетом,
                // без ограничения размера. Это гарантирует, что клиент получит все теги,
                // включая #minecraft:infiniburn_overworld.
                byte[][] registryNames = RegistryTagCatalog.RegistryNames;
                byte[][][] tagNames = RegistryTagCatalog.TagNames;

                for (int i = 0; i < registryNames.Length; i++)
                {
                    // Получаем writer
                    var updateTags = outbound.Begin();
                    
                    updateTags.WriteVarInt(0x0D) // update_tags
                              .WriteVarInt(1)    // одна группа на реестр
                              .WriteString(registryNames[i]);
                              
                    byte[][] tags = tagNames[i];
                    updateTags.WriteVarInt(tags.Length);
                    
                    foreach (byte[] tag in tags)
                    {
                        updateTags.WriteString(tag)
                                  .WriteVarInt(0); // пустой список элементов
                    }
                    
                    // Коммитим пакет
                    outbound.Commit(ref updateTags);
                }

                Logger.Debug(LogKey.PacketUpdateTags);
                return PacketHandleResult.Continue;
            }
            case 2:
            {
                // --- 3. S→C Feature Flags (ID 0x0C) ---
                var featureFlags = outbound.Begin();
                featureFlags.WriteVarInt(0x0C) // update_enabled_features
                            .WriteVarInt(1)
                            .WriteString("minecraft:vanilla");
                outbound.Commit(ref featureFlags);

                // --- 4. S→C Finish Configuration (ID 0x03) ---
                // Снова вызываем Begin, предыдущий writer уже сброшен
                var finishConfiguration = outbound.Begin();
                finishConfiguration.WriteVarInt(0x03); // finish_configuration
                outbound.Commit(ref finishConfiguration);

                Logger.Debug(LogKey.PacketConfigurationFinish);
                return PacketHandleResult.Accepted;
            }
            default:
                return PacketHandleResult.Kick;
        }
    }
}