using Verstack.Shared.Maths;

namespace Verstack.Layers.Realm.Session.World;

/// <summary>
/// Последнее синхронизированное клиентам состояние объекта — точка отсчёта для расчёта
/// diff'а move-пакетов. Сеётся из <see cref="TransformInf"/> при спавне, обновляется каждой
/// отправкой. Хранит всё, что реплицируется: позицию (для delta), поворот (для angle diff),
/// OnGround (просто последнее значение — не diff'ается, всегда актуален).
/// </summary>
internal struct SyncedInf
{
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public bool OnGround;
}