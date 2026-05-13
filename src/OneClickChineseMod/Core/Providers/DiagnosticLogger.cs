using System.IO;
using System.Text;

namespace OneClickChineseMod.Core.Providers;

public static class DiagnosticLogger
{
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppBasicInfoManager.AppEnglishName);
    private static readonly string _diagnosticLogPath = Path.Combine(_logDir, "diagnostic.log");
    private static readonly object _lock = new();

    static DiagnosticLogger()
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            File.AppendAllText(_diagnosticLogPath,
                $"=== SenGameLoc Diagnostic Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}",
                Encoding.UTF8);
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(_diagnosticLogPath, logLine, Encoding.UTF8);
            }
        }
        catch { }
    }
}
