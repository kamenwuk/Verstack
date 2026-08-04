namespace Verstack.Layers.Global;

/// <summary>
/// Игровые параметры мира стадии Play: дальности прорисовки/симуляции и игровой режим
/// при входе. Используются в Login(Play) 0x31 и player_info_update 0x46.
/// </summary>
public static class WorldConstants
{
    /// <summary>
    /// Дальность прорисовки чанков клиентом (chunk-радиус). 2..32.
    /// </summary>
    public const int VIEW_DISTANCE = 10;

    /// <summary>
    /// Дальность симуляции (тиков) на сервере. Чанки дальше этой границы тикаются без ИИ.
    /// </summary>
    public const int SIMULATION_DISTANCE = 10;

    /// <summary>
    /// Игровой режим при входе. 0 survival, 1 creative, 2 adventure, 3 spectator.
    /// </summary>
    public const byte GAME_MODE = 1; // Creative

    /// <summary>
    /// Предыдущий игровой режим. -1 = не определён (для переключения F3+N).
    /// </summary>
    public const sbyte PREVIOUS_GAME_MODE = -1;
}