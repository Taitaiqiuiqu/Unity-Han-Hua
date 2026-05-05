using System.IO;
using System.Reflection;
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
    private readonly string _deepseekTranslate = RESOURCE_PREFIX + "DeepSeekTranslate.dll";
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
    public XUnityVersion CurrentVersion { get; private set; } = XUnityVersion.V5_4_4;

    public DeployResult Deploy(GameInfo gameInfo, bool forceOverwrite = false)
    {
        return Deploy(gameInfo, XUnityVersion.V5_4_4, forceOverwrite);
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

            ConsoleUtils.WriteInfo("正在部署 DeepSeekTranslate 翻译端点...");
            DeployDeepSeekTranslate(gameInfo.GameRoot);

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

    public void DeployDeepSeekTranslate(string gameRoot)
    {
        var pluginDir = Path.Combine(gameRoot, "BepInEx", "plugins", "XUnity.AutoTranslator", "Translators");
        FileUtils.EnsureDirectoryExists(pluginDir);

        var dllPath = Path.Combine(pluginDir, "DeepSeekTranslate.dll");
        CopyEmbeddedResourceToFile(_deepseekTranslate, dllPath);
        ConsoleUtils.WriteSuccess("DeepSeekTranslate 翻译端点已部署到 Translators/");
    }

    public void DeployExtras(string gameRoot, GameType gameType)
    {
        DeployTmpFontAsset(gameRoot);
        DeployDeepSeekTranslate(gameRoot);
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

    public XUnityVersion GetAlternateVersion()
    {
        return CurrentVersion switch
        {
            XUnityVersion.V5_4_4 => XUnityVersion.V5_5_0,
            XUnityVersion.V5_5_0 => XUnityVersion.V5_5_2,
            XUnityVersion.V5_5_2 => XUnityVersion.V5_6_1,
            XUnityVersion.V5_6_1 => XUnityVersion.V5_4_4,
            _ => XUnityVersion.V5_5_0
        };
    }
}
