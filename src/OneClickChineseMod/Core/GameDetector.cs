using System.IO;
using System.Runtime.InteropServices;
using OneClickChineseMod.Models;

namespace OneClickChineseMod.Core;

public class GameDetector
{
    private const ushort MACHINE_X86 = 0x014c;
    private const ushort MACHINE_X64 = 0x8664;

    public GameInfo DetectFromExecutable(string exePath)
    {
        var gameInfo = new GameInfo
        {
            GamePath = exePath,
            GameRoot = Path.GetDirectoryName(exePath) ?? string.Empty,
            GameName = Path.GetFileNameWithoutExtension(exePath)
        };

        gameInfo.Architecture = DetectArchitecture(exePath);
        gameInfo.Type = DetectGameType(gameInfo.GameRoot, gameInfo.GameName);
        gameInfo.UnityVersion = GetUnityVersion(gameInfo.GameRoot, gameInfo.GameName);
        gameInfo.IsBepInExInstalled = IsBepInExInstalled(gameInfo.GameRoot);
        DetectSteamGame(gameInfo);

        if (gameInfo.IsBepInExInstalled)
        {
            gameInfo.InstalledBepInExVersion = GetInstalledBepInExVersion(gameInfo.GameRoot);
        }

        return gameInfo;
    }

    public GameArchitecture DetectArchitecture(string exePath)
    {
        try
        {
            using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read);
            fs.Position = 0x3C;

            var peOffsetBytes = new byte[4];
            fs.ReadExactly(peOffsetBytes, 0, 4);
            var peOffset = BitConverter.ToUInt32(peOffsetBytes, 0);

            fs.Position = peOffset + 0x4;
            var machineBytes = new byte[2];
            fs.ReadExactly(machineBytes, 0, 2);

            var machine = BitConverter.ToUInt16(machineBytes, 0);

            return machine switch
            {
                MACHINE_X86 => GameArchitecture.x86,
                MACHINE_X64 => GameArchitecture.x64,
                _ => GameArchitecture.Unknown
            };
        }
        catch
        {
            return GameArchitecture.Unknown;
        }
    }

    public GameType DetectGameType(string gameRoot, string gameName)
    {
        if (IsIL2CPPGame(gameRoot, gameName))
            return GameType.IL2CPP;

        if (IsMonoGame(gameRoot, gameName))
            return GameType.Mono;

        return GameType.Unknown;
    }

    private bool IsIL2CPPGame(string gameRoot, string gameName)
    {
        var dataDir = gameName + "_Data";
        var searchOption = SearchOption.AllDirectories;

        var gameAssemblyPaths = new[]
        {
            Path.Combine(gameRoot, dataDir, "PlayFab", "GameAssembly.dll"),
            Path.Combine(gameRoot, dataDir, "Managed", "GameAssembly.dll"),
            Path.Combine(gameRoot, dataDir, "il2cpp_data", "Metadata", "global-metadata.dat")
        };

        foreach (var path in gameAssemblyPaths)
        {
            if (File.Exists(path))
                return true;
        }

        try
        {
            var files = Directory.GetFiles(gameRoot, "GameAssembly.dll", searchOption);
            if (files.Length > 0) return true;

            files = Directory.GetFiles(gameRoot, "global-metadata.dat", searchOption);
            if (files.Length > 0) return true;
        }
        catch { }

        return false;
    }

    private bool IsMonoGame(string gameRoot, string gameName)
    {
        var dataDir = gameName + "_Data";
        var managedDir = Path.Combine(gameRoot, dataDir, "Managed");
        if (Directory.Exists(managedDir))
        {
            var assemblyCSharp = Path.Combine(managedDir, "Assembly-CSharp.dll");
            if (File.Exists(assemblyCSharp))
                return true;
        }

        var unityPlayerPath = Path.Combine(gameRoot, "UnityPlayer.dll");
        if (File.Exists(unityPlayerPath))
        {
            return true;
        }

        return false;
    }

    public string GetUnityVersion(string gameRoot, string gameName)
    {
        var dataDir = gameName + "_Data";

        var globalGameManagersPath = Path.Combine(gameRoot, dataDir, "globalgamemanagers");
        if (File.Exists(globalGameManagersPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(globalGameManagersPath);
                var index = IndexOfUnityVersion(bytes);
                if (index >= 0)
                {
                    var endIndex = Array.IndexOf(bytes, (byte)0, index);
                    if (endIndex > index)
                    {
                        return System.Text.Encoding.UTF8.GetString(bytes, index, endIndex - index);
                    }
                }
            }
            catch { }
        }

        var playerConnectionPath = Path.Combine(gameRoot, dataDir, "PlayerConnectionConfigFile");
        if (File.Exists(playerConnectionPath))
        {
            try
            {
                var lines = File.ReadAllLines(playerConnectionPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("UnityVersion="))
                    {
                        return line.Substring("UnityVersion=".Length);
                    }
                }
            }
            catch { }
        }

        return "Unknown";
    }

    private int IndexOfUnityVersion(byte[] data)
    {
        byte[] pattern = System.Text.Encoding.UTF8.GetBytes("m_EditorVersion:");
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i + pattern.Length;
        }
        return -1;
    }

    public bool IsBepInExInstalled(string gameRoot)
    {
        var bepinexCoreDir = Path.Combine(gameRoot, "BepInEx", "core");
        var bepinexPluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins");

        if (!Directory.Exists(bepinexCoreDir) || !Directory.Exists(bepinexPluginsDir))
            return false;

        // 必须验证注入桥接文件存在，仅目录存在不代表 BepInEx 可以被加载。
        // winhttp.dll / version.dll — Mono BepInEx 5.x doorstop
        // doorstop_config.ini     — IL2CPP BepInEx 6.x doorstop
        var winhttp = Path.Combine(gameRoot, "winhttp.dll");
        var version = Path.Combine(gameRoot, "version.dll");
        var doorstop = Path.Combine(gameRoot, "doorstop_config.ini");

        return File.Exists(winhttp) || File.Exists(version) || File.Exists(doorstop);
    }

    private string GetInstalledBepInExVersion(string gameRoot)
    {
        var bepinexCoreDir = Path.Combine(gameRoot, "BepInEx", "core");
        var bepInExDll = Path.Combine(bepinexCoreDir, "BepInEx.dll");

        if (File.Exists(bepInExDll))
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(bepInExDll);
                return versionInfo.FileVersion ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        return "Unknown";
    }

    public bool ValidateGameExecutable(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return false;

        if (!File.Exists(exePath))
            return false;

        var extension = Path.GetExtension(exePath);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public void DetectSteamGame(GameInfo gameInfo)
    {
        var steamDll64 = Path.Combine(gameInfo.GameRoot, "steam_api64.dll");
        var steamDll32 = Path.Combine(gameInfo.GameRoot, "steam_api.dll");

        if (!File.Exists(steamDll64) && !File.Exists(steamDll32))
            return;

        gameInfo.IsSteamGame = true;

        var appIdFile = Path.Combine(gameInfo.GameRoot, "steam_appid.txt");
        if (File.Exists(appIdFile))
        {
            try
            {
                gameInfo.SteamAppId = File.ReadAllText(appIdFile).Trim();
            }
            catch { }
        }
    }
}
