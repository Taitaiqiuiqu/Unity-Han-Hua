using System.IO;
using System.Text;
using OneClickChineseMod.Core.Providers;

namespace OneClickChineseMod.Utils;

public static class HookLogger
{
    private static readonly object _lock = new();
    private static readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppBasicInfoManager.AppEnglishName);
    private static readonly string _hookLogPath = Path.Combine(_logDir, "hooktxt.log");
    private static readonly string _modelLogPath = Path.Combine(_logDir, "modelreturn.log");
    private static bool _initialized = false;

    static HookLogger()
    {
        try
        {
            Directory.CreateDirectory(_logDir);
        }
        catch { }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            File.AppendAllText(_hookLogPath,
                $"=== SenGameLoc Hook Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}",
                Encoding.UTF8);
        }
        catch { }
    }

    public static void Log(string sourceText, string translatedText, string? error = null)
    {
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                if (error != null)
                {
                    File.AppendAllText(_hookLogPath,
                        $"[{timestamp}] [FAIL] 原文: {sourceText}{Environment.NewLine}    错误: {error}{Environment.NewLine}",
                        Encoding.UTF8);
                }
                else
                {
                    File.AppendAllText(_hookLogPath,
                        $"[{timestamp}] [OK] 原文: {sourceText}{Environment.NewLine}    译文: {translatedText}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
        }
        catch { }
    }

    public static void LogModelRawResponse(string requestText, string rawResponse, bool isSuccess)
    {
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var status = isSuccess ? "[OK]" : "[FAIL]";
                File.AppendAllText(_modelLogPath,
                    $"=== {timestamp} {status} ==={Environment.NewLine}" +
                    $"[REQUEST]{Environment.NewLine}{requestText}{Environment.NewLine}" +
                    $"[RESPONSE]{Environment.NewLine}{rawResponse}{Environment.NewLine}" +
                    $"{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch { }
    }
}