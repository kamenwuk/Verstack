namespace Verstack.Layers.Realm.Chunks;

/// <summary>
/// Постоянный ECS-компонент области загрузки чанков игрока (живёт всю сессию — от handoff
/// до дисконнекта; суффикс <c>...Inf</c> по конвенции времени жизни компонентов проекта).
///
/// <para>Viewport — прямоугольная область, удерживаемая загруженной вокруг игрока:
/// квадрат <c>(2R+1)×(2R+1)</c> чанков с центром <see cref="CenterX"/>/<see cref="CenterZ"/>.
/// При смещении игрока через границу чанка система edge-update сдвигает центр и подсчитывает
/// дельту — фронт (вошедшие чанки) и тыл (вышедшие).</para>
///
/// <para>Сеётся в <c>HandoffApprovalSystem</c> при входе игрока с центром (0, 0) и начальным
/// радиусом <see cref="INITIAL_RADIUS"/>. Предыдущий центр отдельно не хранится: edge-update
/// вычисляет дельту за один проход, держа старый центр в локальной переменной до перезаписи.</para>
/// </summary>
public struct ChunkViewportInf
{
    /// <summary>
    /// Начальный радиус viewport'а при seeding'е — сетка 5×5, согласовано с
    /// <c>JoinChunkBatchBundle</c>. Временно живёт здесь; после стабилизации подсистемы
    /// чанков переедет в конфигурацию мира. Render distance клиента (<c>WorldConstants.VIEW_DISTANCE</c>)
    /// — отдельное понятие и здесь не используется.
    /// </summary>
    public const int INITIAL_RADIUS = 2;

    /// <summary>Центр области загрузки по X, в чанк-координатах.</summary>
    public int CenterX;

    /// <summary>Центр области загрузки по Z, в чанк-координатах.</summary>
    public int CenterZ;

    /// <summary>Радиус области загрузки: квадрат <c>(2R+1)×(2R+1)</c> чанков вокруг центра.</summary>
    public int Radius;
}