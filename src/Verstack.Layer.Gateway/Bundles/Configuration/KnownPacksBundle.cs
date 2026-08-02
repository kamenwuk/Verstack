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
                            using var nbtData = NbtAssetSource.RentScoped(
                                NbtCatalog.WorldGen, 
                                NbtAsset.DimensionTypes, 
                                "overworld" // Убедись, что файл называется overworld.nbt
                            );
                            registryData.WriteSpan(nbtData.Data);
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