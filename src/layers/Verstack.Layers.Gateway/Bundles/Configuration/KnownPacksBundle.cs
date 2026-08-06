using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Packet.Inbound;
using Verstack.Shared.Nbt.Writer;
using Verstack.Layers.Global;
using Verstack.Shared.Assets;
using Verstack.Shared.Debug;
using Verstack.Shared.Nbt;
using Leopotam.EcsProto;

namespace Verstack.Layers.Gateway.Bundles;

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
                    throw new InvalidOperationException("Длины массивов идентификаторов реестров и обязательных записей не совпадают.");

                // Временные буферы для NBT
                Span<NbtFrame> nbtFrames = stackalloc NbtFrame[32];
                Span<byte> nbtBuffer = stackalloc byte[512];

                for (int i = 0; i < syncedIds.Length; i++)
                {
                    byte[] registryId = syncedIds[i];
                    byte[][] entries = entryIds[i];

                    var registryData = outbound.Begin();
                    
                    registryData.WriteVarInt(0x07) // registry_data
                                .WriteString(registryId)
                                .WriteVarInt(entries.Length);

                    foreach (byte[] entryName in entries)
                    {
                        registryData.WriteString(entryName);

                        bool isDimensionType = i == (int)SyncedRegistryCatalog.RegistryType.DimensionType;
                        bool isOverworld = isDimensionType && entryName.SequenceEqual("minecraft:overworld"u8);

                        bool isBiome = i == (int)SyncedRegistryCatalog.RegistryType.Biome;
                        bool isPlains = isBiome && entryName.SequenceEqual("minecraft:plains"u8);

                        if (isOverworld)
                        {
                            registryData.WriteBool(true);
                            using var nbtData = AssetSource.RentScoped(
                                AssetCatalog.WorldGen, 
                                AssetType.DimensionTypes, 
                                "overworld", 
                                AssetExtension.Nbt
                            );
                            registryData.WriteSpan(nbtData.Data);
                        }
                        else if (isPlains)
                        {
                            // Отправляем NBT для биома plains
                            registryData.WriteBool(true);
                            
                            var nbtWriter = new NbtStreamWriter(nbtBuffer, nbtFrames, networked: true);
                            nbtWriter.BeginRootCompound();
                            nbtWriter.WriteByte("has_precipitation"u8, 1);
                            nbtWriter.WriteFloat("temperature"u8, 0.8f);
                            nbtWriter.WriteFloat("downfall"u8, 0.4f);
                            
                            nbtWriter.BeginCompound("effects"u8);
                            nbtWriter.WriteInt("sky_color"u8, 7907327);
                            nbtWriter.WriteInt("water_color"u8, 4159204);
                            nbtWriter.WriteInt("water_fog_color"u8, 329011);
                            nbtWriter.WriteInt("fog_color"u8, 12638463);
                            nbtWriter.EndCompound();
                            
                            nbtWriter.EndCompound();
                            
                            ReadOnlySpan<byte> biomeNbt = nbtWriter.Finish();
                            registryData.WriteSpan(biomeNbt);
                        }
                        else
                        {
                            // Нет данных для других записей
                            registryData.WriteBool(false);
                        }
                    }
                    
                    outbound.Commit(ref registryData);
                }

                return PacketHandleResult.Continue;
            }
            case 1:
            {
                // --- 2. S→C Update Tags (ID 0x0D) ---
                var updateTags = outbound.Begin();
    
                updateTags.WriteVarInt(0x0D); // update_tags
    
                // ПОЛУЧАЕМ ВСЕ ТЕГИ ИЗ КЭША (включая timeline, damage_type и т.д.)
                AssetSource.TagBatchEntry[] tagBatch = AssetSource.GetTagBatch();
    
                // Пишем количество реестров с тегами
                updateTags.WriteVarInt(tagBatch.Length);
    
                // Итерируемся по всем реестрам
                foreach (AssetSource.TagBatchEntry entry in tagBatch)
                {
                    // 1. Имя реестра (например "minecraft:timeline")
                    updateTags.WriteString(entry.RegistryId.Span);
        
                    // 2. Сами теги (включая пустые, чтобы переопределить ванильные)
                    updateTags.WriteSpan(entry.Data.Span);
                }
    
                outbound.Commit(ref updateTags);

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