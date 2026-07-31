using System.Diagnostics;

namespace Verstack.Lifecycle;

public class ServerTime
{
    private static readonly double TickFrequency = 1.0 / Stopwatch.Frequency;
    private long _lastTickTimestamp = Stopwatch.GetTimestamp();
    
    /// <summary>
    /// Время, затраченное на выполнение предыдущего тика (в секундах).
    /// </summary>
    public double DeltaTime { get; private set; } = 0;

    /// <summary>
    /// Общее время работы сервера (в секундах).
    /// Считается от запуска, абсолютная точность без дрейфа.
    /// </summary>
    public double TotalTime { get; private set; } = 0;

    public void Update()
    {
        long currentTimestamp = Stopwatch.GetTimestamp();
        long elapsedTicks = currentTimestamp - _lastTickTimestamp;
            
        // Переводим тики процессора в секунды
        DeltaTime = elapsedTicks * TickFrequency;
            
        // Общее время считаем напрямую от старта, чтобы избежать накопления погрешности (дрейфа)
        TotalTime = currentTimestamp * TickFrequency;
            
        _lastTickTimestamp = currentTimestamp;
    }
}