namespace Verstack.Debug;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}

public static class Logger
{
    private static readonly object _consoleLock = new object();

    // Теперь params спокойно стоит последним
    public static void Info(LogKey key, params object[] args) 
        => Log(LogLevel.Info, key, args);

    public static void Warn(LogKey key, params object[] args) 
        => Log(LogLevel.Warning, key, args);

    // Для ошибок с исключением делаем отдельный метод (без params)
    public static void Error(LogKey key, Exception ex = null) 
        => Log(LogLevel.Error, key, null, ex);

    // А для текстовых ошибок оставляем params
    public static void Error(LogKey key, params object[] args) 
        => Log(LogLevel.Error, key, args);

    public static void Debug(LogKey key, params object[] args) 
        => Log(LogLevel.Debug, key, args);

    // Исправляем сигнатуру (теперь всё совпадает)
    private static void Log(LogLevel level, LogKey key, object[] args, Exception ex = null)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        int threadId = Environment.CurrentManagedThreadId;
        
        // Достаем строку из словаря
        string message = LogLocale.Get(key, args);
        
        // Если есть исключение, добавляем его в лог
        if (ex != null)
        {
            message += $"\n{ex}";
        }

        string levelStr = level.ToString();
    
        // 7 — это длина самого длинного слова (Warning)
        // Формат ",-7" означает: выровнять по левому краю, добив пробелами до 7 символов
        string paddedLevel = $"{levelStr,-7}";

        string logLine = $"[{time}] [Thread {threadId}] [{paddedLevel}] {message}";

        lock (_consoleLock)
        {
            ConsoleColor prevColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Info => ConsoleColor.DarkCyan,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Debug => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };
            
            Console.WriteLine(logLine);
            Console.ForegroundColor = prevColor;
        }
    }
}