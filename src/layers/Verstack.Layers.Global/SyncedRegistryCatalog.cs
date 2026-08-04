namespace Verstack.Layers.Global;

/// <summary>
/// Каталог синхронизированных реестров Minecraft 26.2.
/// </summary>
public static class SyncedRegistryCatalog
{
    /// <summary>
    /// Массив байтовых идентификаторов реестров.
    /// </summary>
    public static readonly byte[][] RegistryIds =
    [
        "minecraft:worldgen/biome"u8.ToArray(),          // 0
        "minecraft:chat_type"u8.ToArray(),               // 1
        "minecraft:trim_pattern"u8.ToArray(),            // 2
        "minecraft:trim_material"u8.ToArray(),           // 3
        "minecraft:wolf_variant"u8.ToArray(),            // 4
        "minecraft:wolf_sound_variant"u8.ToArray(),      // 5
        "minecraft:pig_variant"u8.ToArray(),             // 6
        "minecraft:pig_sound_variant"u8.ToArray(),       // 7
        "minecraft:frog_variant"u8.ToArray(),            // 8
        "minecraft:cat_variant"u8.ToArray(),             // 9
        "minecraft:cat_sound_variant"u8.ToArray(),       // 10
        "minecraft:cow_sound_variant"u8.ToArray(),       // 11
        "minecraft:cow_variant"u8.ToArray(),             // 12
        "minecraft:chicken_sound_variant"u8.ToArray(),   // 13
        "minecraft:chicken_variant"u8.ToArray(),         // 14
        "minecraft:zombie_nautilus_variant"u8.ToArray(), // 15
        "minecraft:painting_variant"u8.ToArray(),        // 16
        "minecraft:sulfur_cube_archetype"u8.ToArray(),   // 17
        "minecraft:dimension_type"u8.ToArray(),          // 18
        "minecraft:damage_type"u8.ToArray(),             // 19
        "minecraft:banner_pattern"u8.ToArray(),          // 20
        "minecraft:enchantment"u8.ToArray(),             // 21
        "minecraft:jukebox_song"u8.ToArray(),            // 22
        "minecraft:instrument"u8.ToArray(),              // 23
        "minecraft:test_environment"u8.ToArray(),        // 24
        "minecraft:test_instance"u8.ToArray(),           // 25
        "minecraft:dialog"u8.ToArray(),                  // 26
        "minecraft:world_clock"u8.ToArray(),             // 27
        "minecraft:timeline"u8.ToArray()                 // 28
    ];

    /// <summary>
    /// Массив обязательных записей для каждого реестра.
    /// </summary>
    public static readonly byte[][][] MandatoryEntries =
    [
        // 0  worldgen/biome
        [
            "minecraft:plains"u8.ToArray()
        ],
        // 1  chat_type (Optional)
        [],
        // 2  trim_pattern (Optional)
        [],
        // 3  trim_material (See below)
        [
            "minecraft:amethyst"u8.ToArray(), "minecraft:copper"u8.ToArray(), "minecraft:diamond"u8.ToArray(), "minecraft:emerald"u8.ToArray(),
            "minecraft:gold"u8.ToArray(), "minecraft:iron"u8.ToArray(), "minecraft:lapis"u8.ToArray(), "minecraft:netherite"u8.ToArray(),
            "minecraft:quartz"u8.ToArray(), "minecraft:redstone"u8.ToArray(), "minecraft:resin"u8.ToArray()
        ],
        // 4  wolf_variant (Non-empty)
        [
            "minecraft:ashen"u8.ToArray(), "minecraft:black"u8.ToArray(), "minecraft:chestnut"u8.ToArray(),
            "minecraft:pale"u8.ToArray(), "minecraft:rusty"u8.ToArray(), "minecraft:snowy"u8.ToArray(),
            "minecraft:spotted"u8.ToArray(), "minecraft:striped"u8.ToArray(), "minecraft:woods"u8.ToArray()
        ],
        // 5  wolf_sound_variant (Non-empty)
        [
            "minecraft:angry"u8.ToArray(), "minecraft:big"u8.ToArray(), "minecraft:classic"u8.ToArray(),
            "minecraft:cute"u8.ToArray(), "minecraft:grumpy"u8.ToArray(), "minecraft:puglin"u8.ToArray(),
            "minecraft:sad"u8.ToArray()
        ],
        // 6  pig_variant (Non-empty)
        [
            "minecraft:cold"u8.ToArray(), "minecraft:temperate"u8.ToArray(), "minecraft:warm"u8.ToArray()
        ],
        // 7  pig_sound_variant (Non-empty)
        [
            "minecraft:big"u8.ToArray(), "minecraft:classic"u8.ToArray(), "minecraft:mini"u8.ToArray()
        ],
        // 8  frog_variant (Non-empty)
        [
            "minecraft:cold"u8.ToArray(), "minecraft:temperate"u8.ToArray(), "minecraft:warm"u8.ToArray()
        ],
        // 9  cat_variant (Non-empty)
        [
            "minecraft:all_black"u8.ToArray(), "minecraft:black"u8.ToArray(), "minecraft:british_shorthair"u8.ToArray(),
            "minecraft:calico"u8.ToArray(), "minecraft:jellie"u8.ToArray(), "minecraft:persian"u8.ToArray(),
            "minecraft:ragdoll"u8.ToArray(), "minecraft:red"u8.ToArray(), "minecraft:siamese"u8.ToArray(),
            "minecraft:tabby"u8.ToArray(), "minecraft:white"u8.ToArray()
        ],
        // 10 cat_sound_variant (Non-empty)
        [
            "minecraft:classic"u8.ToArray(), "minecraft:royal"u8.ToArray()
        ],
        // 11 cow_sound_variant (Non-empty)
        [
            "minecraft:classic"u8.ToArray(), "minecraft:moody"u8.ToArray()
        ],
        // 12 cow_variant (Non-empty)
        [
            "minecraft:cold"u8.ToArray(), "minecraft:temperate"u8.ToArray(), "minecraft:warm"u8.ToArray()
        ],
        // 13 chicken_sound_variant (Non-empty)
        [
            "minecraft:classic"u8.ToArray(), "minecraft:picky"u8.ToArray()
        ],
        // 14 chicken_variant (See below)
        [
            "minecraft:cold"u8.ToArray(), "minecraft:temperate"u8.ToArray(), "minecraft:warm"u8.ToArray()
        ],
        // 15 zombie_nautilus_variant (Non-empty)
        [
            "minecraft:temperate"u8.ToArray(), "minecraft:warm"u8.ToArray()
        ],
        // 16 painting_variant (Non-empty)
        [
            "minecraft:kebab"u8.ToArray(), "minecraft:aztec"u8.ToArray(), "minecraft:alban"u8.ToArray(),
            "minecraft:aztec2"u8.ToArray(), "minecraft:bomb"u8.ToArray(), "minecraft:plant"u8.ToArray(),
            "minecraft:wasteland"u8.ToArray(), "minecraft:pool"u8.ToArray(), "minecraft:courbet"u8.ToArray(),
            "minecraft:sea"u8.ToArray(), "minecraft:sunset"u8.ToArray(), "minecraft:creebet"u8.ToArray(),
            "minecraft:wanderer"u8.ToArray(), "minecraft:graham"u8.ToArray(), "minecraft:match"u8.ToArray(),
            "minecraft:bust"u8.ToArray(), "minecraft:stage"u8.ToArray(), "minecraft:void"u8.ToArray(),
            "minecraft:skull_and_roses"u8.ToArray(), "minecraft:wither"u8.ToArray(), "minecraft:fighters"u8.ToArray(),
            "minecraft:pointer"u8.ToArray(), "minecraft:pigscene"u8.ToArray(), "minecraft:burning_skull"u8.ToArray(),
            "minecraft:skeleton"u8.ToArray(), "minecraft:earth"u8.ToArray(), "minecraft:wind"u8.ToArray(),
            "minecraft:water"u8.ToArray(), "minecraft:fire"u8.ToArray(), "minecraft:donkey_kong"u8.ToArray()
        ],
        // 17 sulfur_cube_archetype (Non-empty)
        [],
        // 18 dimension_type (See below)
        [
            "minecraft:overworld"u8.ToArray() // <--- ОБЯЗАТЕЛЬНО РАСКОММЕНТИРОВАТЬ!
        ],
        // 20 damage_type (See below)
        [
            "minecraft:in_fire"u8.ToArray(), "minecraft:lightning_bolt"u8.ToArray(), "minecraft:on_fire"u8.ToArray(), "minecraft:lava"u8.ToArray(),
            "minecraft:hot_floor"u8.ToArray(), "minecraft:in_wall"u8.ToArray(), "minecraft:cramming"u8.ToArray(), "minecraft:drown"u8.ToArray(),
            "minecraft:starve"u8.ToArray(), "minecraft:cactus"u8.ToArray(), "minecraft:fall"u8.ToArray(), "minecraft:fly_into_wall"u8.ToArray(),
            "minecraft:out_of_world"u8.ToArray(), "minecraft:generic"u8.ToArray(), "minecraft:magic"u8.ToArray(), "minecraft:wither"u8.ToArray(),
            "minecraft:dragon_breath"u8.ToArray(), "minecraft:dry_out"u8.ToArray(), "minecraft:sweet_berry_bush"u8.ToArray(),
            "minecraft:freeze"u8.ToArray(), "minecraft:stalagmite"u8.ToArray(), "minecraft:falling_stalactite"u8.ToArray(),
            "minecraft:sting"u8.ToArray(), "minecraft:mob_attack"u8.ToArray(), "minecraft:mob_attack_no_aggro"u8.ToArray(),
            "minecraft:mob_projectile"u8.ToArray(), "minecraft:thorns"u8.ToArray(), "minecraft:explosion"u8.ToArray(),
            "minecraft:player_explosion"u8.ToArray(), "minecraft:sonic_boom"u8.ToArray(), "minecraft:bad_respawn_point"u8.ToArray(),
            "minecraft:outside_border"u8.ToArray(), "minecraft:generic_kill"u8.ToArray(), "minecraft:wind_charge"u8.ToArray(),
            "minecraft:mace_smash"u8.ToArray(), "minecraft:ender_pearl"u8.ToArray(),
            "minecraft:spear"u8.ToArray(), "minecraft:campfire"u8.ToArray() // <--- ДОБАВЛЕНО ДЛЯ 26.2
        ],
        // 20 banner_pattern (See below)
        [
            "minecraft:flow"u8.ToArray(),
            "minecraft:globe"u8.ToArray(),
            "minecraft:guster"u8.ToArray(),
          //  "minecraft:bordure_indented"u8.ToArray(),
         //   "minecraft:field_masoned"u8.ToArray(),
            "minecraft:creeper"u8.ToArray(),
            "minecraft:skull"u8.ToArray(),
            "minecraft:flower"u8.ToArray(),
            "minecraft:mojang"u8.ToArray(),
            "minecraft:piglin"u8.ToArray()
        ],
        // 21 enchantment (Optional)
        [],
        // 22 jukebox_song (See below)
        [
            "minecraft:13"u8.ToArray(), "minecraft:cat"u8.ToArray(), "minecraft:blocks"u8.ToArray(), "minecraft:chirp"u8.ToArray(),
            "minecraft:far"u8.ToArray(), "minecraft:mall"u8.ToArray(), "minecraft:mellohi"u8.ToArray(), "minecraft:stal"u8.ToArray(),
            "minecraft:strad"u8.ToArray(), "minecraft:ward"u8.ToArray(), "minecraft:11"u8.ToArray(), "minecraft:wait"u8.ToArray(),
            "minecraft:otherside"u8.ToArray(), "minecraft:5"u8.ToArray(), "minecraft:pigstep"u8.ToArray(), "minecraft:relic"u8.ToArray(),
            "minecraft:creator"u8.ToArray(), "minecraft:creator_music_box"u8.ToArray(), "minecraft:precipice"u8.ToArray(),
            "minecraft:tears"u8.ToArray(), "minecraft:bounce"u8.ToArray(), "minecraft:lava_chicken"u8.ToArray()
        ],
        // 23 instrument (See below)
        [
            "minecraft:ponder_goat_horn"u8.ToArray()
        ],
        // 24 test_environment (Optional)
        [],
        // 25 test_instance (Optional)
        [],
        // 26 dialog (Optional)
        [],
        // 27 world_clock (Optional)
        [
            "minecraft:overworld"u8.ToArray()
        ],
        // 28 timeline (Optional)
        []
    ];

    /// <summary>
    /// Прослойка: возвращает ID реестра по его типу.
    /// </summary>
    public static byte[] GetId(RegistryType type) => RegistryIds[(int)type];

    /// <summary>
    /// Прослойка: возвращает обязательные записи реестра по его типу.
    /// </summary>
    public static byte[][] GetEntries(RegistryType type) => MandatoryEntries[(int)type];

    /// <summary>
    /// Перечисление всех синхронизированных типов реестров.
    /// </summary>
    public enum RegistryType : byte
    {
        Biome = 0,
        ChatType = 1,
        TrimPattern = 2,
        TrimMaterial = 3,
        WolfVariant = 4,
        WolfSoundVariant = 5,
        PigVariant = 6,
        PigSoundVariant = 7,
        FrogVariant = 8,
        CatVariant = 9,
        CatSoundVariant = 10,
        CowSoundVariant = 11,
        CowVariant = 12,
        ChickenSoundVariant = 13,
        ChickenVariant = 14,
        ZombieNautilusVariant = 15,
        PaintingVariant = 16,
        SulfurCubeArchetype = 17,
        DimensionType = 18,
        DamageType = 19,
        BannerPattern = 20,
        Enchantment = 21,
        JukeboxSong = 22,
        Instrument = 23,
        TestEnvironment = 24,
        TestInstance = 25,
        Dialog = 26,
        WorldClock = 27,
        Timeline = 28,
    }
}