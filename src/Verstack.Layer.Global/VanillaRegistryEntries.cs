namespace Verstack.Layer.Global;

/// <summary>
/// Canonical entry-ids для 13 обязательных synced-реестров Minecraft 26.2.
/// Эти реестры требуют ≥1 entry (клиент 26.2 валидирует non-empty): все variant-реестры +
/// <c>painting_variant</c>. Остальные 16 synced-реестров допускают count=0 и здесь представлены
/// пустым массивом.
///
/// Index-aligned с <see cref="VanillaSyncedRegistries.SyncedRegistryIds"/>:
/// <see cref="EntryIds"/>[i] соответствует <c>SyncedRegistryIds</c>[i].
///
/// Источник: bundled-datapack <c>minecraft-26.2-client.jar</c>,
/// пути <c>data/minecraft/&lt;registry&gt;/&lt;entry&gt;.json</c>. Порядок внутри реестра —
/// alphabetical (клиент сортирует сам, фиксируем для детерминизма и сверки с research-файлом).
/// Разбор: <c>.zcode/research/mandatory-registry-entries-26.2.md</c>.
/// </summary>
public static class VanillaRegistryEntries
{
    /// <summary>
    /// Entry-ids каждого synced-реестра как UTF-8 байты. Index-aligned с
    /// <see cref="VanillaSyncedRegistries.SyncedRegistryIds"/>. Пустой массив = реестр без
    /// обязательных entries (count=0 в listing). GC-free: hot path Configuration читает по индексу.
    /// </summary>
    public static readonly byte[][][] EntryIds =
    [
        [],                                                 // ← 0  worldgen/biome
        [],                                                 // ← 1  chat_type
        [],                                                 // ← 2  trim_pattern
        [],                                                 // ← 3  trim_material
        [                                                   // ← 4  wolf_variant (9)
            "minecraft:ashen"u8.ToArray(),
            "minecraft:black"u8.ToArray(),
            "minecraft:chestnut"u8.ToArray(),
            "minecraft:pale"u8.ToArray(),
            "minecraft:rusty"u8.ToArray(),
            "minecraft:snowy"u8.ToArray(),
            "minecraft:spotted"u8.ToArray(),
            "minecraft:striped"u8.ToArray(),
            "minecraft:woods"u8.ToArray()
        ],
        [                                                   // ← 5  wolf_sound_variant (7)
            "minecraft:angry"u8.ToArray(),
            "minecraft:big"u8.ToArray(),
            "minecraft:classic"u8.ToArray(),
            "minecraft:cute"u8.ToArray(),
            "minecraft:grumpy"u8.ToArray(),
            "minecraft:puglin"u8.ToArray(),
            "minecraft:sad"u8.ToArray()
        ],
        [                                                   // ← 6  pig_variant (3)
            "minecraft:cold"u8.ToArray(),
            "minecraft:temperate"u8.ToArray(),
            "minecraft:warm"u8.ToArray()
        ],
        [                                                   // ← 7  pig_sound_variant (3)
            "minecraft:big"u8.ToArray(),
            "minecraft:classic"u8.ToArray(),
            "minecraft:mini"u8.ToArray()
        ],
        [                                                   // ← 8  frog_variant (3)
            "minecraft:cold"u8.ToArray(),
            "minecraft:temperate"u8.ToArray(),
            "minecraft:warm"u8.ToArray()
        ],
        [                                                   // ← 9  cat_variant (11)
            "minecraft:all_black"u8.ToArray(),
            "minecraft:black"u8.ToArray(),
            "minecraft:british_shorthair"u8.ToArray(),
            "minecraft:calico"u8.ToArray(),
            "minecraft:jellie"u8.ToArray(),
            "minecraft:persian"u8.ToArray(),
            "minecraft:ragdoll"u8.ToArray(),
            "minecraft:red"u8.ToArray(),
            "minecraft:siamese"u8.ToArray(),
            "minecraft:tabby"u8.ToArray(),
            "minecraft:white"u8.ToArray()
        ],
        [                                                   // ← 10 cat_sound_variant (2)
            "minecraft:classic"u8.ToArray(),
            "minecraft:royal"u8.ToArray()
        ],
        [                                                   // ← 11 cow_sound_variant (2)
            "minecraft:classic"u8.ToArray(),
            "minecraft:moody"u8.ToArray()
        ],
        [                                                   // ← 12 cow_variant (3)
            "minecraft:cold"u8.ToArray(),
            "minecraft:temperate"u8.ToArray(),
            "minecraft:warm"u8.ToArray()
        ],
        [                                                   // ← 13 chicken_sound_variant (2)
            "minecraft:classic"u8.ToArray(),
            "minecraft:picky"u8.ToArray()
        ],
        [                                                   // ← 14 chicken_variant (3)
            "minecraft:cold"u8.ToArray(),
            "minecraft:temperate"u8.ToArray(),
            "minecraft:warm"u8.ToArray()
        ],
        [                                                   // ← 15 zombie_nautilus_variant (2)
            "minecraft:temperate"u8.ToArray(),
            "minecraft:warm"u8.ToArray()
        ],
        [                                                   // ← 16 painting_variant (51)
            "minecraft:alban"u8.ToArray(),
            "minecraft:aztec"u8.ToArray(),
            "minecraft:aztec2"u8.ToArray(),
            "minecraft:backyard"u8.ToArray(),
            "minecraft:baroque"u8.ToArray(),
            "minecraft:bomb"u8.ToArray(),
            "minecraft:bouquet"u8.ToArray(),
            "minecraft:burning_skull"u8.ToArray(),
            "minecraft:bust"u8.ToArray(),
            "minecraft:cavebird"u8.ToArray(),
            "minecraft:changing"u8.ToArray(),
            "minecraft:cotan"u8.ToArray(),
            "minecraft:courbet"u8.ToArray(),
            "minecraft:creebet"u8.ToArray(),
            "minecraft:dennis"u8.ToArray(),
            "minecraft:donkey_kong"u8.ToArray(),
            "minecraft:earth"u8.ToArray(),
            "minecraft:endboss"u8.ToArray(),
            "minecraft:fern"u8.ToArray(),
            "minecraft:fighters"u8.ToArray(),
            "minecraft:finding"u8.ToArray(),
            "minecraft:fire"u8.ToArray(),
            "minecraft:graham"u8.ToArray(),
            "minecraft:humble"u8.ToArray(),
            "minecraft:kebab"u8.ToArray(),
            "minecraft:lowmist"u8.ToArray(),
            "minecraft:match"u8.ToArray(),
            "minecraft:meditative"u8.ToArray(),
            "minecraft:orb"u8.ToArray(),
            "minecraft:owlemons"u8.ToArray(),
            "minecraft:passage"u8.ToArray(),
            "minecraft:pigscene"u8.ToArray(),
            "minecraft:plant"u8.ToArray(),
            "minecraft:pointer"u8.ToArray(),
            "minecraft:pond"u8.ToArray(),
            "minecraft:pool"u8.ToArray(),
            "minecraft:prairie_ride"u8.ToArray(),
            "minecraft:sea"u8.ToArray(),
            "minecraft:skeleton"u8.ToArray(),
            "minecraft:skull_and_roses"u8.ToArray(),
            "minecraft:stage"u8.ToArray(),
            "minecraft:sunflowers"u8.ToArray(),
            "minecraft:sunset"u8.ToArray(),
            "minecraft:tides"u8.ToArray(),
            "minecraft:unpacked"u8.ToArray(),
            "minecraft:void"u8.ToArray(),
            "minecraft:wanderer"u8.ToArray(),
            "minecraft:wasteland"u8.ToArray(),
            "minecraft:water"u8.ToArray(),
            "minecraft:wind"u8.ToArray(),
            "minecraft:wither"u8.ToArray()
        ],
        [],                                                 // ← 17 sulfur_cube_archetype
        [],                                                 // ← 18 dimension_type
        [],                                                 // ← 19 damage_type
        [],                                                 // ← 20 banner_pattern
        [],                                                 // ← 21 enchantment
        [],                                                 // ← 22 jukebox_song
        [],                                                 // ← 23 instrument
        [],                                                 // ← 24 test_environment
        [],                                                 // ← 25 test_instance
        [],                                                 // ← 26 dialog
        [],                                                 // ← 27 world_clock
        []                                                  // ← 28 timeline
    ];
}