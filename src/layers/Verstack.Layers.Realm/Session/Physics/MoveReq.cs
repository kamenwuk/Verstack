using Verstack.Shared.Maths;

namespace Verstack.Layers.Realm.Movement;

/// <summary>
/// Запрос на перемещение: накопленный за тик ввод от клиента (Set Player Position 0x1E,
/// Set Player Position And Rotation 0x1F, Set Player Rotation 0x20). Снимается системой-обработчиком
/// после чтения в том же тике — живёт ровно один проход.
///
/// <para>За тик может прийти несколько пакетов вперемешку (движение + поворот на месте). Чтобы
/// пакет без позиции (0x20) не обнулил Position, слияние идёт по флагам: <see cref="HasPosition"/>
/// и <see cref="HasRotation"/> — каждый затирает только свою часть.</para>
/// </summary>
public struct MoveReq
{
    /// <summary>Мировая позиция стоп игрока (block units, float — из double wire-формата). Валидна при <see cref="HasPosition"/>.</summary>
    public Vector3 Position;

    /// <summary>Yaw (поворот вокруг Y), градусы. Валиден при <see cref="HasRotation"/>.</summary>
    public float Yaw;

    /// <summary>Pitch (наклон), градусы. Валиден при <see cref="HasRotation"/>.</summary>
    public float Pitch;

    /// <summary>Несёт ли запрос позицию (пакеты 0x1E/0x1F). 0x20 — только поворот, позицию не трогает.</summary>
    public bool HasPosition;

    /// <summary>Несёт ли запрос вращение (пакеты 0x1F/0x20). 0x1E — только позицию.</summary>
    public bool HasRotation;

    /// <summary>На земле ли игрок (из onGround-флага move-пакетов). Для анимаций ходьбы в репликации.</summary>
    public bool OnGround;
}