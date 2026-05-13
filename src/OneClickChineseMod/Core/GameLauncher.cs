using System.Diagnostics;
using System.IO;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public class GameLauncher
{
    public Process? LaunchGame(GameInfo gameInfo, bool preferSteam = false)
    {
        // NOTE: 强制使用直接启动而非 Steam URL 方案。
        // Steam URL (steam://rungameid/) 会让 Steam 从自身的安装目录 (steamapps/common)
        // 启动游戏 exe，但 BepInEx 被部署到了用户选择的 exe 所在目录。
        // 两者目录不一致时，winhttp.dll 闸门 DLL 不会被加载，XUnity 永远无法连接代理。
        // 直接启动 exe 能保证工作目录 == BepInEx 部署目录，且 Steam 仍会进行 DRM 校验。
        if (gameInfo.IsSteamGame && preferSteam)
        {
            ConsoleUtils.WriteWarning("检测到 Steam 游戏，为兼容 BepInEx 注入将使用直接启动...");
        }

        return LaunchDirect(gameInfo.GameRoot, gameInfo.GameName + ".exe");
    }

    private static Process? LaunchViaSteam(GameInfo gameInfo)
    {
        if (string.IsNullOrWhiteSpace(gameInfo.SteamAppId))
        {
            ConsoleUtils.WriteWarning("未找到 Steam AppID，无法通过 Steam 启动");
            return null;
        }

        try
        {
            var steamUrl = $"steam://rungameid/{gameInfo.SteamAppId}";
            ConsoleUtils.WriteInfo($"通过 Steam 启动游戏 (AppID: {gameInfo.SteamAppId})...");

            var startInfo = new ProcessStartInfo
            {
                FileName = steamUrl,
                UseShellExecute = true
            };

            return Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"Steam 启动失败: {ex.Message}");
            return null;
        }
    }

    private static Process? LaunchDirect(string gameRoot, string gameExe)
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
