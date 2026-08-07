namespace Verstack.Layers.Realm.Chunks;

/// <summary>
/// Постоянный ECS-компонент области загрузки чанков игрока (живёт всю сессию — от handoff
/// до дисконнекта; суффикс <c>...Inf</c> по конвенции времени жизни компонентов проекта).
///
/// <para>Viewport — прямоугольная область, удерживаемая загруженной: квадрат
/// <c>(2R+1)×(2R+1)</c> чанков. <see cref="LastCenterX"/>/<see cref="LastCenterZ"/> — центр,
/// применённый при последнем edge-update; текущий центр выводится из <c>TransformInf</c>
/// на лету и сравнивается с последним применённым. При несовпадении
/// <c>ChunkViewportUpdater.Update</c> считает дельту (фронт/тыл) и обновляет
/// <see cref="LastCenterX"/>/<see cref="LastCenterZ"/>.</para>
///
/// <para>Сеётся в <c>HandoffApprovalSystem</c> при входе игрока с центром (0, 0) и начальным
/// радиусом <see cref="INITIAL_RADIUS"/>.</para>
/// </summary>
public struct ChunkViewportInf
{
    /// <summary>
    /// Начальный радиус viewport'а при seeding'е — сетка 5×5, согласовано с
    /// <c>JoinChunkBatchBundle</c>. Временно живёт здесь; после стабилизации подсистемы
    /// чанков переедет в конфигурацию мира. Render distance клиента
    /// (<c>WorldConstants.VIEW_DISTANCE</c>) — отдельное понятие и здесь не используется.
    /// </summary>
    public const int INITIAL_RADIUS = 2;

    /// <summary>Центр области загрузки по X, применённый при последнем edge-update.</summary>
    public int LastCenterX;

    /// <summary>Центр области загрузки по Z, применённый при последнем edge-update.</summary>
    public int LastCenterZ;

    /// <summary>Радиус области загрузки: квадрат <c>(2R+1)×(2R+1)</c> чанков вокруг центра.</summary>
    public int Radius;
}