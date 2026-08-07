using Verstack.Shared.Maths;

namespace Verstack.Layers.Realm.Movement;

/// <summary>
/// Запрос на перемещение: накопленный за тик ввод от клиента (Set Player Position 0x1E,
/// Set Player Position And Rotation 0x1F). Снимается системой-обработчиком после чтения
/// в том же тике — живёт ровно один проход.
///
/// <para>За тик может прийти несколько пакетов: последний перетирает предыдущие (важна
/// финальная позиция). Хранит мировые координаты стоп игрока; чанк-координаты
/// вычисляются делением на 16 с floor. Yaw/pitch — для будущей rotation-sync.</para>
/// </summary>
public struct MoveReq
{
    /// <summary>Мировая позиция стоп игрока (block units, float — из double wire-формата).</summary>
    public Vector3 Position;

    /// <summary>Yaw (поворот вокруг Y), градусы. Валиден при <see cref="HasRotation"/>.</summary>
    public float Yaw;

    /// <summary>Pitch (наклон), градусы. Валиден при <see cref="HasRotation"/>.</summary>
    public float Pitch;

    /// <summary>Несёт ли запрос вращение (пакет 0x1F); 0x1E — только позиция.</summary>
    public bool HasRotation;
}