namespace Verstack.Layer.Global;

/// <summary>
/// Canonical набор synced-реестров Minecraft 26.2 для Clientbound Registry Data (S→C 0x07).
/// Источник: bytecode <c>net.minecraft.resources.RegistryDataLoader.SYNCHRONIZED_REGISTRIES</c>
/// (29 элементов, static-блок <c>&lt;clinit&gt;</c>), извлечён статически через <c>javap</c>.
/// Порядок элементов = порядок отправки packet'ов клиенту в фазе Configuration.
/// </summary>
/// <remarks>
/// Контринтуитивное: Java-поле <c>Registries.BIOME</c> разворачивается в
/// <c>minecraft:worldgen/biome</c> (с namespace-path <c>worldgen/</c>), а не в
/// <c>minecraft:biome</c>. Все остальные 28 реестров — без префикса. Поэтому список выведен из
/// bytecode, а не из имён полей — наивное <c>fieldName.ToLower()</c> даст неверный идентификатор.
/// Полный разбор и сверка — <c>.zcode/research/vanilla-26.2-synced-registries.md</c>.
/// </remarks>
public static class VanillaSyncedRegistries
{
    /// <summary>
    /// Идентификаторы synced-реестров 26.2 как готовые UTF-8 байты (без VarInt-префикса).
    /// Один массив на реестр — длины переменные (10..25 байт). Инициализируется один раз при
    /// первом обращении (static-init), далее читается zero-alloc: горячий путь Configuration
    /// копирует байты в <c>SpanWriter</c> без аллокаций.
    /// </summary>
    public static readonly byte[][] SyncedRegistryIds =
    [
        "minecraft:worldgen/biome"u8.ToArray(),          // ← 0  единственный с префиксом worldgen/
        "minecraft:chat_type"u8.ToArray(),               // ← 1
        "minecraft:trim_pattern"u8.ToArray(),            // ← 2
        "minecraft:trim_material"u8.ToArray(),           // ← 3
        "minecraft:wolf_variant"u8.ToArray(),            // ← 4
        "minecraft:wolf_sound_variant"u8.ToArray(),      // ← 5
        "minecraft:pig_variant"u8.ToArray(),             // ← 6
        "minecraft:pig_sound_variant"u8.ToArray(),       // ← 7
        "minecraft:frog_variant"u8.ToArray(),            // ← 8
        "minecraft:cat_variant"u8.ToArray(),             // ← 9
        "minecraft:cat_sound_variant"u8.ToArray(),       // ← 10
        "minecraft:cow_sound_variant"u8.ToArray(),       // ← 11
        "minecraft:cow_variant"u8.ToArray(),             // ← 12
        "minecraft:chicken_sound_variant"u8.ToArray(),   // ← 13
        "minecraft:chicken_variant"u8.ToArray(),         // ← 14
        "minecraft:zombie_nautilus_variant"u8.ToArray(), // ← 15
        "minecraft:painting_variant"u8.ToArray(),        // ← 16
        "minecraft:sulfur_cube_archetype"u8.ToArray(),   // ← 17
        "minecraft:dimension_type"u8.ToArray(),          // ← 18
        "minecraft:damage_type"u8.ToArray(),             // ← 19
        "minecraft:banner_pattern"u8.ToArray(),          // ← 20
        "minecraft:enchantment"u8.ToArray(),             // ← 21
        "minecraft:jukebox_song"u8.ToArray(),            // ← 22
        "minecraft:instrument"u8.ToArray(),              // ← 23
        "minecraft:test_environment"u8.ToArray(),        // ← 24
        "minecraft:test_instance"u8.ToArray(),           // ← 25
        "minecraft:dialog"u8.ToArray(),                  // ← 26
        "minecraft:world_clock"u8.ToArray(),             // ← 27
        "minecraft:timeline"u8.ToArray()                 // ← 28
    ];
}