namespace Verstack.Layers.Realm.Movement;

/// <summary>
/// Запрос на перемещение: накопленный за тик ввод от клиента (Set Player Position 0x1E,
/// Set Player Position And Rotation 0x1F). Снимается системой-обработчиком после чтения
/// в том же тике — живёт ровно один проход.
///
/// За тик может прийти несколько пакетов: последний перетирает предыдущие (важна финальная
/// позиция). Хранит мировые координаты стоп игрока; чанк-координаты вычисляются делением на 16
/// с floor. Yaw/pitch — для будущей rotation-sync.
/// </summary>
public struct MoveReq
{
    public double X;
    public double Y;
    public double Z;
    public float Yaw;
    public float Pitch;
    public bool HasRotation;
}