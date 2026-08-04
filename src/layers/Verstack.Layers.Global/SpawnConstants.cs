namespace Verstack.Layers.Global;

/// <summary>
/// Точка появления игрока: блок спавна (для компаса, 0x61) и позиция стоп игрока
/// (телепорт входа, 0x48).
///
/// Стопы игрока выводятся из блока спавна (+1 по Y), чтобы компас и точка стояния
/// никогда не разъезжались. Surface Y=64 совпадает с верхним слоем камня FlatGenerator
/// (секция 8, локальный Y=0) — игрок встаёт прямо на поверхность, без проваливания.
/// </summary>
public static class SpawnConstants
{
    /// <summary>
    /// Тип и имя измерения, в котором находится точка спавна. Пока зашит один мир — Overworld.
    /// </summary>
    public const string DIMENSION_NAME = "minecraft:overworld";

    /// <summary>
    /// ID типа измерения в реестре minecraft:dimension_type (0 = minecraft:overworld).
    /// Соответствует индексу в Registry Data, отправленном в Configuration.
    /// </summary>
    public const int DIMENSION_TYPE_ID = 0;

    /// <summary>
    /// Координаты блока точки спавна (для set_default_spawn_position 0x61, компас).
    /// </summary>
    public const int SPAWN_BLOCK_X = 8;
    public const int SPAWN_BLOCK_Y = 64; // верхняя поверхность камня в FlatGenerator
    public const int SPAWN_BLOCK_Z = 8;

    /// <summary>
    /// Yaw/Pitch точки спавна в градусах (0/0 = смотрит на юг, прямо).
    /// </summary>
    public const float SPAWN_YAW = 0.0f;
    public const float SPAWN_PITCH = 0.0f;
}