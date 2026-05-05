using System.Diagnostics;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public class GameLauncher
{
    public Process? LaunchGame(string gameRoot, string gameExe)
    {
        var fullExePath = Path.Combine(gameRoot, gameExe);
        if (!File.Exists(fullExePath))
        {
            fullExePath = gameExe;
        }

        if (!File.Exists(fullExePath))
        {
            ConsoleUtils.WriteError($"找不到游戏可执行文件: {fullExePath}");
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fullExePath,
                WorkingDirectory = gameRoot,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                ConsoleUtils.WriteSuccess($"游戏进程已启动 (PID: {process.Id})");
            }
            return process;
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"启动游戏失败: {ex.Message}");
            return null;
        }
    }
}
