using Verstack.Shared.Maths;

namespace Verstack.Shared.Spatial.Orientation;

/// <summary>
/// Предоставляет утилиты для работы с пространственной ориентацией, направлениями (8 сторон) и координатами.
/// </summary>
public static class Compass
{
    /// <summary>
    /// Количество секторов (направлений) в системе координат компаса.
    /// </summary>
    public const int SECTORS = 8;
    
    /// <summary>
    /// Массив всех возможных направлений компаса в порядке обхода по часовой стрелке.
    /// </summary>
    public static readonly Dir8[] Directions =
    [
        Dir8.North,
        Dir8.Northeast,
        Dir8.East,
        Dir8.Southeast,
        Dir8.South,
        Dir8.Southwest,
        Dir8.West,
        Dir8.Northwest
    ];
    
    /// <summary>
    /// Массив целочисленных векторов, соответствующих направлениям компаса.
    /// </summary>
    public static readonly Vector2Int[] Axes =
    [
        new(0, -1), // North  — Minecraft-конвенция: north = −Z
        new(1, -1), // Northeast
        new(1, 0),  // East
        new(1, 1),  // Southeast
        new(0, 1),  // South
        new(-1, 1), // Southwest
        new(-1, 0), // West
        new(-1, -1) // Northwest
    ];
    
    /// <summary>
    /// Массив нормализованных векторов (приблизительно 0.7 для диагоналей), соответствующих направлениям компаса.
    /// </summary>
    public static readonly Vector2[] UnitAxes =
    [
        new(0, 1),    // North
        new(0.70f, 0.70f), // Northeast
        new(1, 0),    // East
        new(0.70f, -0.70f), // Southeast
        new(0, -1),   // South
        new(-0.70f, -0.70f), // Southwest
        new(-1, 0),   // West
        new(-0.70f, 0.70f) // Northwest
    ];

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static Dir8 GetAxis(Vector2Int from, Vector2Int to)
    // {
    //     var delta = math.sign(to - from);
    //     
    //     return (delta.x, delta.y) switch
    //     {
    //         (0, 1) => Dir8.North,
    //         (1, 1) => Dir8.Northeast,
    //         (1, 0) => Dir8.East,
    //         (1, -1) => Dir8.Southeast,
    //         (0, -1) => Dir8.South,
    //         (-1, -1) => Dir8.Southwest,
    //         (-1, 0) => Dir8.West,
    //         (-1, 1) => Dir8.Northwest,
    //         _ => throw new ArgumentException($"[{nameof(Compass)}] Не удается получить направление от {from} до {to}: позиции равны")
    //     };
    // }

    // /// <summary>
    // /// Определяет направление (Dir8) от одной float-координаты к другой.
    // /// </summary>
    // /// <param name="from">Начальная позиция.</param>
    // /// <param name="to">Конечная позиция.</param>
    // /// <returns>Направление типа <see cref="Dir8"/>.</returns>
    // /// <exception cref="ArgumentException">Выбрасывается, если позиции from и to совпадают.</exception>
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static Dir8 GetAxis(Vector2 from, Vector2 to)
    // {
    //     var delta = math.sign(to - from);
    //     
    //     return (delta.x, delta.y) switch
    //     {
    //         (0, 1) => Dir8.North,
    //         (1, 1) => Dir8.Northeast,
    //         (1, 0) => Dir8.East,
    //         (1, -1) => Dir8.Southeast,
    //         (0, -1) => Dir8.South,
    //         (-1, -1) => Dir8.Southwest,
    //         (-1, 0) => Dir8.West,
    //         (-1, 1) => Dir8.Northwest,
    //         _ => throw new ArgumentException($"[{nameof(Compass)}] Не удается получить направление от {from} до {to}: позиции равны")
    //     };
    // }
    
//     /// <summary>
//     /// Возвращает нормализованный шаг (вектор направления) от одной позиции к другой.
//     /// </summary>
//     /// <param name="from">Текущая позиция.</param>
//     /// <param name="to">Целевая позиция.</param>
//     /// <returns>Вектор направления (int2) со значениями -1, 0 или 1.</returns>
//     [MethodImpl(MethodImplOptions.AggressiveInlining)]
//     public static Vector2Int StepToward(Vector2Int from, Vector2Int to)
//     {
//         var delta = to - from;
// #if DEBUG_LOGGER
//         if (delta.Equals(int2.zero))
//             throw new ArgumentException($"[{nameof(Compass)}] Не удается получить направление от {from} до {to}: позиции равны");
// #endif
//         return math.sign(delta);
//     }
    
    /// <summary>
    /// Перечисление 8 направлений (сторон света и диагонали).
    /// </summary>
    public enum Dir8 : byte
    {
        /// <summary>
        /// ↑ (0,1)
        /// </summary>
        North = 0,
        /// <summary>
        /// ↗ (1,1)
        /// </summary>
        Northeast = 1,
        /// <summary>
        /// → (1,0)
        /// </summary>
        East = 2,
        /// <summary>
        /// ↘ (1,-1)
        /// </summary>
        Southeast = 3,
        /// <summary>
        /// ↓ (0,-1)
        /// </summary>
        South = 4,
        /// <summary>
        /// ↙ (-1,-1)
        /// </summary>
        Southwest = 5,
        /// <summary>
        /// ← (-1,0)
        /// </summary>
        West = 6,
        /// <summary>
        /// ↖ (-1,1)
        /// </summary>
        Northwest = 7
    }

    /// <summary>
    /// Битовая маска для направлений <see cref="Dir8"/>. Позволяет комбинировать несколько направлений.
    /// </summary>
    [Flags]
    public enum Dir8Mask : byte
    {
        None = 0,
        /// <summary>
        /// ↑ (0,1)
        /// </summary>
        North = 1 << 0,
        /// <summary>
        /// ↗ (1,1)
        /// </summary>
        Northeast = 1 << 1,
        /// <summary>
        /// → (1,0)
        /// </summary>
        East = 1 << 2,
        /// <summary>
        /// ↘ (1,-1)
        /// </summary>
        Southeast = 1 << 3,
        /// <summary>
        /// ↓ (0,-1)
        /// </summary>
        South = 1 << 4,
        /// <summary>
        /// ↙ (-1,-1)
        /// </summary>
        Southwest = 1 << 5,
        /// <summary>
        /// ← (-1,0)
        /// </summary>
        West = 1 << 6,
        /// <summary>
        /// ↖ (-1,1)
        /// </summary>
        Northwest = 1 << 7,
        All = 255
    }

    public readonly struct Dir8InOut(Dir8 direction, bool incoming)
    {
        public Dir8 Direction => direction;
        public bool Incoming => incoming;
        public bool Outgoing => !incoming;
    }
    
    public readonly struct Dir8Opt
    {
        public static Dir8Opt Empty => new Dir8Opt(Dir8.North, false);
        public bool HasValue => _hasValue;

        public Compass.Dir8 Value
        {
            get
            {
#if DEBUG_LOGGER
                if(_hasValue == false)
                    throw new InvalidOperationException($"[{nameof(Compass.Dir8Opt)}] Значение пустое.");
#endif
                return _value;
            }
        }
    
        private readonly Compass.Dir8 _value;
        private readonly bool _hasValue;

        
        private Dir8Opt(Dir8 value, bool hasValue)
        {
            _value = value;
            _hasValue = hasValue;
        }

        public Dir8Opt(Dir8 value) : this(value, true) { }
    }
}
