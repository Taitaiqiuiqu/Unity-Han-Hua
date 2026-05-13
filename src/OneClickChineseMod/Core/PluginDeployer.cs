using System.IO;
using System.Reflection;
using System.Text;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public enum XUnityVersion
{
    V5_4_4,
    V5_5_0,
    V5_5_2,
    V5_6_1
}

public class PluginDeployer
{
    private const string RESOURCE_PREFIX = "OneClickChineseMod.Resources.";

    private readonly string _bepinexMonoX64 = RESOURCE_PREFIX + "BepInEx_win_x64_5.4.23.4.zip";
    private readonly string _bepinexMonoX86 = RESOURCE_PREFIX + "BepInEx_win_x86_5.4.23.4.zip";
    private readonly string _bepinexIl2cppX64 = RESOURCE_PREFIX + "BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a.zip";
    private readonly string _bepinexIl2cppX86 = RESOURCE_PREFIX + "BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.755+3fab71a.zip";
    private readonly string _tmpFontBundle = RESOURCE_PREFIX + "TMP_Font_AssetBundles.zip";

    private readonly Dictionary<XUnityVersion, (string mono, string il2cpp)> _xunityVersions = new()
    {
        [XUnityVersion.V5_4_4] = (
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-5.4.4.zip",
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-IL2CPP-5.4.4.zip"
        ),
        [XUnityVersion.V5_5_0] = (
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-5.5.0.zip",
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-IL2CPP-5.5.0.zip"
        ),
        [XUnityVersion.V5_5_2] = (
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-5.5.2.zip",
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-IL2CPP-5.5.2.zip"
        ),
        [XUnityVersion.V5_6_1] = (
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-5.6.1.zip",
            RESOURCE_PREFIX + "XUnity.AutoTranslator-BepInEx-IL2CPP-5.6.1.zip"
        )
    };

    public static readonly XUnityVersion[] AvailableVersions = { XUnityVersion.V5_4_4, XUnityVersion.V5_5_0, XUnityVersion.V5_5_2, XUnityVersion.V5_6_1 };
    public XUnityVersion CurrentVersion { get; private set; } = XUnityVersion.V5_5_2;

    public DeployResult Deploy(GameInfo gameInfo, bool forceOverwrite = false)
    {
        return Deploy(gameInfo, XUnityVersion.V5_5_2, forceOverwrite);
    }

    public DeployResult Deploy(GameInfo gameInfo, XUnityVersion xunityVersion, bool forceOverwrite = false)
    {
        var result = new DeployResult();

        if (!FileUtils.HasWritePermission(gameInfo.GameRoot))
        {
            result.Status = DeployStatus.Failed;
            result.Message = "没有写入权限，请以管理员身份运行";
            return result;
        }

        if (gameInfo.IsBepInExInstalled && !forceOverwrite)
        {
            result.Status = DeployStatus.AlreadyInstalled;
            result.Message = "检测到已安装 BepInEx，使用 --force 强制覆盖";
            return result;
        }

        try
        {
            var bepinexZip = GetBepinexZipPath(gameInfo);

            ConsoleUtils.WriteInfo($"正在部署 BepInEx ({GetBepinexVersionName(gameInfo)})...");
            DeployZipToGameRoot(bepinexZip, gameInfo.GameRoot);

            ConsoleUtils.WriteInfo($"正在部署 XUnity.AutoTranslator ({xunityVersion})...");
            DeployXUnityToGame(gameInfo.GameRoot, gameInfo.Type, xunityVersion);

            ConsoleUtils.WriteInfo("正在部署 TMP 中文字体...");
            DeployTmpFontAsset(gameInfo.GameRoot);

            result.Status = DeployStatus.Success;
            result.Message = "插件部署成功";
        }
        catch (Exception ex)
        {
            result.Status = DeployStatus.Failed;
            result.Message = $"部署失败: {ex.Message}";
        }

        return result;
    }

    public void DeployXUnityOnly(string gameRoot, GameType gameType, XUnityVersion version)
    {
        ConsoleUtils.WriteInfo($"正在部署 XUnity.AutoTranslator ({version})...");
        DeployXUnityToGame(gameRoot, gameType, version);
    }

    public void DeployTmpFontAsset(string gameRoot)
    {
        DeployZipToGameRoot(_tmpFontBundle, gameRoot);
        ConsoleUtils.WriteSuccess("TMP 中文字体资产已部署到 AutoTranslator/");
    }

    public void DeployExtras(string gameRoot, GameType gameType)
    {
        DeployTmpFontAsset(gameRoot);
    }

    private void DeployXUnityToGame(string gameRoot, GameType gameType, XUnityVersion version)
    {
        var (monoZip, il2cppZip) = _xunityVersions[version];
        var zipName = gameType == GameType.IL2CPP ? il2cppZip : monoZip;
        DeployZipToGameRoot(zipName, gameRoot);
        CurrentVersion = version;
    }

    private string GetBepinexZipPath(GameInfo gameInfo)
    {
        if (gameInfo.Type == GameType.Unknown)
            ConsoleUtils.WriteWarning("未检测到游戏类型 (Mono/IL2CPP)，默认使用 Mono 版本。如游戏实际为 IL2CPP 请手动指定。");

        return (gameInfo.Type, gameInfo.Architecture) switch
        {
            (GameType.IL2CPP, GameArchitecture.x64) => _bepinexIl2cppX64,
            (GameType.IL2CPP, GameArchitecture.x86) => _bepinexIl2cppX86,
            (GameType.Mono, GameArchitecture.x64) => _bepinexMonoX64,
            (GameType.Mono, GameArchitecture.x86) => _bepinexMonoX86,
            (GameType.Unknown, GameArchitecture.x64) => _bepinexMonoX64,
            (GameType.Unknown, GameArchitecture.x86) => _bepinexMonoX86,
            _ => _bepinexMonoX64
        };
    }

    private string GetBepinexVersionName(GameInfo gameInfo)
    {
        return (gameInfo.Type, gameInfo.Architecture) switch
        {
            (GameType.IL2CPP, _) => "6.0.0 (IL2CPP)",
            (GameType.Mono, GameArchitecture.x86) => "5.4.23.4 (x86 Mono)",
            (GameType.Mono, GameArchitecture.x64) => "5.4.23.4 (x64 Mono)",
            _ => "5.4.23.4"
        };
    }

    private void DeployZipToGameRoot(string resourceName, string gameRoot)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"无法找到嵌入资源: {resourceName}");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        FileUtils.SafeExtractZip(memoryStream, gameRoot);
    }

    private void CopyEmbeddedResourceToFile(string resourceName, string targetPath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"无法找到嵌入资源: {resourceName}");

        using var fileStream = File.Create(targetPath);
        stream.CopyTo(fileStream);
    }

    public bool ValidateInstallation(string gameRoot, GameType gameType = GameType.Mono)
    {
        var requiredFiles = gameType == GameType.IL2CPP
            ? new[]
            {
                Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.dll"),
                Path.Combine(gameRoot, "BepInEx", "plugins"),
                Path.Combine(gameRoot, "doorstop_config.ini")
            }
            : new[]
            {
                Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.dll"),
                Path.Combine(gameRoot, "BepInEx", "plugins"),
                Path.Combine(gameRoot, "winhttp.dll")
            };

        foreach (var file in requiredFiles)
        {
            if (File.Exists(file)) continue;
            if (Directory.Exists(file)) continue;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证 XUnity 插件 DLL 是否存在于 BepInEx/plugins 目录中
    /// </summary>
    public bool FindXUnityAssembly(string gameRoot, out string? foundPath, out string? errorDetail)
    {
        foundPath = null;
        errorDetail = null;

        var pluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsDir))
        {
            errorDetail = $"BepInEx/plugins 目录不存在: {pluginsDir}";
            return false;
        }

        // 搜索 XUnity.AutoTranslator 相关 DLL
        try
        {
            var xunityFiles = Directory.GetFiles(pluginsDir, "XUnity.AutoTranslator*.dll", SearchOption.AllDirectories);
            if (xunityFiles.Length == 0)
            {
                errorDetail = $"在 {pluginsDir} 下未找到 XUnity.AutoTranslator 插件 DLL，这意味着 XUnity 插件未正确部署。";
                return false;
            }

            foundPath = xunityFiles[0];
            return true;
        }
        catch (Exception ex)
        {
            errorDetail = $"搜索 XUnity 插件时出错: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 输出诊断指引，告知用户如何检查 BepInEx/XUnity 是否被游戏加载
    /// </summary>
    public string GetDiagnosticGuide(GameInfo gameInfo)
    {
        var logFileName = gameInfo.Type == GameType.IL2CPP ? "LogOutput.txt" : "LogOutput.log";
        var logPath = Path.Combine(gameInfo.GameRoot, "BepInEx", logFileName);
        var xunityConfigPath = Path.Combine(gameInfo.GameRoot, "BepInEx", "config", "AutoTranslatorConfig.ini");

        var sb = new StringBuilder();
        sb.AppendLine("===== 诊断指引 =====");
        sb.AppendLine($"1. 检查 BepInEx 日志: {logPath}");
        sb.AppendLine("   - 如果文件不存在: BepInEx 未成功加载，请确认 winhttp.dll/version.dll 在游戏根目录");
        sb.AppendLine("   - 如果文件存在，搜索 'XUnity' 或 'AutoTranslator': 确认插件已加载");
        sb.AppendLine($"2. 验证插件配置: {xunityConfigPath}");
        sb.AppendLine("   - 确认 [Custom] 下的 Url=http://127.0.0.1:5588/translate");
        sb.AppendLine("3. 确认游戏不包含反作弊/防注入保护");

        return sb.ToString();
    }

    public XUnityVersion GetAlternateVersion()
    {
        return CurrentVersion switch
        {
            XUnityVersion.V5_4_4 => XUnityVersion.V5_5_2,
            XUnityVersion.V5_5_0 => XUnityVersion.V5_5_2,
            XUnityVersion.V5_5_2 => XUnityVersion.V5_6_1,
            XUnityVersion.V5_6_1 => XUnityVersion.V5_5_2,
            _ => XUnityVersion.V5_5_2
        };
    }
}
