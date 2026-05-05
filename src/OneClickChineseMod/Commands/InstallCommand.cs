using OneClickChineseMod.Core;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Commands;

public class InstallCommand
{
    private readonly GameDetector _gameDetector;
    private readonly PluginDeployer _pluginDeployer;
    private readonly ConfigGenerator _configGenerator;
    private readonly GameLauncher _gameLauncher;

    private bool _forceOverwrite = false;
    private bool _skipLaunch = false;
    private bool _verbose = false;
    private bool _dryRun = false;

    public InstallCommand()
    {
        _gameDetector = new GameDetector();
        _pluginDeployer = new PluginDeployer();
        _configGenerator = new ConfigGenerator(AppConfig.Load());
        _gameLauncher = new GameLauncher();
    }

    public void Execute(string[] args)
    {
        var gamePath = ExtractGamePath(args);
        ParseArguments(args);

        if (string.IsNullOrEmpty(gamePath))
        {
            ShowHelp();
            return;
        }

        if (!File.Exists(gamePath))
        {
            ConsoleUtils.WriteError($"游戏文件不存在: {gamePath}");
            return;
        }

        if (_dryRun)
            ConsoleUtils.WriteWarning("=== 模拟运行模式 (--dry-run)，不会实际修改任何文件 ===");

        ConsoleUtils.WriteInfo($"正在检测游戏: {gamePath}");

        if (!_gameDetector.ValidateGameExecutable(gamePath))
        {
            ConsoleUtils.WriteError("无效的游戏可执行文件");
            return;
        }

        var gameInfo = _gameDetector.DetectFromExecutable(gamePath);

        ConsoleUtils.WriteLine();
        ShowGameInfo(gameInfo);
        ConsoleUtils.WriteLine();

        if (gameInfo.IsBepInExInstalled && !_forceOverwrite)
        {
            ConsoleUtils.WriteWarning("检测到已安装 BepInEx，跳过安装步骤");
            ConsoleUtils.WriteInfo("如需重新安装请使用 --force 参数");
            ConsoleUtils.WriteLine();
        }
        else if (!_dryRun)
        {
            ConsoleUtils.WriteInfo("开始部署插件...");

            var deployResult = _pluginDeployer.Deploy(gameInfo, _forceOverwrite);

            if (deployResult.Status == DeployStatus.Failed)
            {
                ConsoleUtils.WriteError(deployResult.Message);
                return;
            }

            if (deployResult.Status == DeployStatus.AlreadyInstalled && !_forceOverwrite)
            {
                ConsoleUtils.WriteWarning(deployResult.Message);
                return;
            }

            ConsoleUtils.WriteSuccess(deployResult.Message);

            ConsoleUtils.WriteInfo("正在生成配置文件...");
            _configGenerator.GenerateConfigs(gameInfo.GameRoot, gameInfo);

            ConsoleUtils.WriteLine();
            VerifyInstallation(gameInfo.GameRoot, gameInfo.Type);
            ConsoleUtils.WriteLine();
            ConsoleUtils.WriteSuccess("安装完成！");
        }
        else
        {
            ConsoleUtils.WriteInfo("模拟运行完成，未进行任何修改。");
            if (!_skipLaunch)
            {
                ConsoleUtils.WriteLine();
                OfferToLaunch(gameInfo);
            }
            else
            {
                WaitForExit();
            }
            return;
        }

        if (!_skipLaunch)
        {
            ConsoleUtils.WriteLine();
            OfferToLaunch(gameInfo);
        }
        else
        {
            ConsoleUtils.WriteInfo("安装已完成 (跳过启动)。");
            WaitForExit();
        }
    }

    private void VerifyInstallation(string gameRoot, GameType gameType)
    {
        ConsoleUtils.WriteInfo("=== 安装验证 ===");

        var checks = gameType == GameType.IL2CPP
            ? new (string, string)[]
            {
                ("BepInEx 目录", Path.Combine(gameRoot, "BepInEx")),
                ("BepInEx 核心", Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.dll")),
                ("BepInEx 插件目录", Path.Combine(gameRoot, "BepInEx", "plugins")),
                ("Doorstop 配置", Path.Combine(gameRoot, "doorstop_config.ini")),
                ("AutoTranslator 配置", Path.Combine(gameRoot, "BepInEx", "config", "AutoTranslatorConfig.ini")),
            }
            : new (string, string)[]
            {
                ("BepInEx 目录", Path.Combine(gameRoot, "BepInEx")),
                ("BepInEx 核心", Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.dll")),
                ("BepInEx 插件目录", Path.Combine(gameRoot, "BepInEx", "plugins")),
                ("winhttp.dll 入口", Path.Combine(gameRoot, "winhttp.dll")),
                ("AutoTranslator 配置", Path.Combine(gameRoot, "BepInEx", "config", "AutoTranslatorConfig.ini")),
            };

        var allOk = true;
        foreach (var (name, path) in checks)
        {
            bool exists = File.Exists(path) || Directory.Exists(path);
            if (exists)
                ConsoleUtils.WriteDebug($"  ✅ {name}: {path}");
            else
            {
                ConsoleUtils.WriteError($"  ❌ {name}: 未找到");
                allOk = false;
            }
        }

        if (allOk)
        {
            ConsoleUtils.WriteSuccess("所有关键文件已就位");
        }
        else
        {
            ConsoleUtils.WriteLine();
            ConsoleUtils.WriteError("部分文件缺失，BepInEx 可能无法正常加载");
            ConsoleUtils.WriteInfo("请尝试以管理员身份运行本程序后使用 --force 重新安装");
        }
    }

    private void OfferToLaunch(GameInfo gameInfo)
    {
        Console.Write("是否立即启动游戏？[Y/n] ");
        var launchChoice = Console.ReadLine()?.Trim() ?? "y";
        if (string.IsNullOrEmpty(launchChoice) ||
            launchChoice.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            launchChoice.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            _gameLauncher.LaunchGame(gameInfo.GameRoot, gameInfo.GameName + ".exe");
        }
        else
        {
            ConsoleUtils.WriteInfo("你可以稍后手动启动游戏。");
            WaitForExit();
        }
    }

    private void ShowGameInfo(GameInfo gameInfo)
    {
        ConsoleUtils.WriteInfo("=== 游戏信息 ===");
        ConsoleUtils.WriteInfo($"游戏名称: {gameInfo.GameName}");
        ConsoleUtils.WriteInfo($"游戏路径: {gameInfo.GamePath}");
        ConsoleUtils.WriteInfo($"游戏目录: {gameInfo.GameRoot}");
        ConsoleUtils.WriteInfo($"架构: {gameInfo.Architecture}");
        ConsoleUtils.WriteInfo($"类型: {gameInfo.Type}");

        if (!string.IsNullOrEmpty(gameInfo.UnityVersion) && gameInfo.UnityVersion != "Unknown")
        {
            ConsoleUtils.WriteInfo($"Unity 版本: {gameInfo.UnityVersion}");
        }

        if (gameInfo.IsBepInExInstalled)
        {
            ConsoleUtils.WriteWarning($"BepInEx 已安装 (版本: {gameInfo.InstalledBepInExVersion})");
        }
        else
        {
            ConsoleUtils.WriteInfo("BepInEx: 未安装");
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("OneClickChineseMod - 一键汉化工具");
        Console.WriteLine();
        Console.WriteLine("用法: OneClickChineseMod.exe install <game.exe> [选项]");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --force, -f        强制覆盖已安装的插件");
        Console.WriteLine("  --skip-launch       安装完成后不启动游戏");
        Console.WriteLine("  --dry-run           模拟运行，不实际修改文件");
        Console.WriteLine("  --verbose, -v       显示详细日志");
        Console.WriteLine("  --help, -h          显示帮助信息");
    }

    private string ExtractGamePath(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith("-"))
                return arg;
        }
        return string.Empty;
    }

    private void ParseArguments(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--force":
                case "-f":
                    _forceOverwrite = true;
                    break;
                case "--skip-launch":
                    _skipLaunch = true;
                    break;
                case "--dry-run":
                    _dryRun = true;
                    break;
                case "--verbose":
                case "-v":
                    _verbose = true;
                    ConsoleUtils.SetVerbose(true);
                    break;
                case "--help":
                case "-h":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        if (_verbose)
        {
            ConsoleUtils.WriteDebug("已开启详细日志模式");
        }
    }

    private void WaitForExit()
    {
        ConsoleUtils.WriteLine();
        ConsoleUtils.WriteInfo("按任意键退出...");
        Console.ReadKey(true);
    }
}
