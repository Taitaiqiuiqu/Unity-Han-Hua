namespace OneClickChineseMod.Utils;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success,
    Debug
}

public class LogEntry
{
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public static class ConsoleUtils
{
    private static bool _verbose = false;

    public static event Action<LogEntry>? OnLog;

    public static void SetVerbose(bool verbose)
    {
        _verbose = verbose;
    }

    private static void Emit(LogLevel level, string message)
    {
        var entry = new LogEntry { Level = level, Message = message };
        OnLog?.Invoke(entry);
    }

    public static void WriteInfo(string message)
    {
        Emit(LogLevel.Info, message);
    }

    public static void WriteWarning(string message)
    {
        Emit(LogLevel.Warning, message);
    }

    public static void WriteError(string message)
    {
        Emit(LogLevel.Error, message);
    }

    public static void WriteSuccess(string message)
    {
        Emit(LogLevel.Success, message);
    }

    public static void WriteDebug(string message)
    {
        if (_verbose)
            Emit(LogLevel.Debug, message);
    }

    public static void WriteProgress(int current, int total, string message)
    {
        Emit(LogLevel.Info, $"[{current}/{total}] {message}");
    }

    public static void WriteLine()
    {
        Emit(LogLevel.Info, "");
    }
}
