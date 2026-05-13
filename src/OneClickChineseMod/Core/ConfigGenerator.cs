using System.IO;
using System.Text;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public class ConfigGenerator
{
    private readonly AppConfig _appConfig;

    public ConfigGenerator(AppConfig appConfig)
    {
        _appConfig = appConfig;
    }

    public void GenerateConfigs(string gameRoot, GameInfo gameInfo)
    {
        GenerateAutoTranslatorConfig(gameRoot);
        ConsoleUtils.WriteSuccess("AutoTranslator 配置文件已生成");

        GenerateDoorstopConfig(gameRoot, gameInfo);
        ConsoleUtils.WriteSuccess("doorstop 配置已就绪");
    }

    private void GenerateAutoTranslatorConfig(string gameRoot)
    {
        var configDir = Path.Combine(gameRoot, "BepInEx", "config");
        var translationDir = Path.Combine(gameRoot, "AutoTranslator", "Translation");

        FileUtils.EnsureDirectoryExists(configDir);
        FileUtils.EnsureDirectoryExists(translationDir);

        var configPath = Path.Combine(configDir, "AutoTranslatorConfig.ini");

        var configContent = GenerateConfigContent();
        // ⚠ 必须使用无 BOM 的 UTF-8，否则 XUnity 的 Mono 端 .ini 解析器会忽略 [Service]/[Custom] 段
        File.WriteAllText(configPath, configContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void GenerateDoorstopConfig(string gameRoot, GameInfo gameInfo)
    {
        if (gameInfo.Type != GameType.IL2CPP)
        {
            ConsoleUtils.WriteDebug("非 IL2CPP 游戏，跳过 doorstop_config.ini 生成");
            return;
        }

        var path = Path.Combine(gameRoot, "doorstop_config.ini");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, GenerateDoorstopConfigContent_IL2CPP(), Encoding.UTF8);
            ConsoleUtils.WriteDebug("已生成 doorstop_config.ini (IL2CPP)");
            return;
        }

        // 文件已存在时必须验证 enabled=true，否则 BepInEx 不注入，整条翻译链路中断。
        var content = File.ReadAllText(path);
        if (content.Contains("enabled=false", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUtils.WriteWarning("doorstop_config.ini 中检测到 enabled=false，正在修复为 enabled=true...");
            var fixedContent = System.Text.RegularExpressions.Regex.Replace(
                content, @"enabled\s*=\s*false", "enabled=true",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            File.WriteAllText(path, fixedContent, Encoding.UTF8);
            ConsoleUtils.WriteSuccess("doorstop_config.ini 已修复");
        }
        else
        {
            ConsoleUtils.WriteDebug("doorstop_config.ini 已存在且 enabled=true，跳过生成");
        }
    }

    private string GenerateDoorstopConfigContent_IL2CPP()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[General]");
        sb.AppendLine("enabled=true");
        sb.AppendLine("redirectOutputLog=false");
        sb.AppendLine();
        sb.AppendLine("[Il2Cpp]");
        sb.AppendLine("interpreter=");
        return sb.ToString();
    }

    private string GenerateConfigContent()
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Service]");
        sb.AppendLine("Endpoint=CustomTranslate");
        sb.AppendLine("FallbackEndpoint=");
        sb.AppendLine();
        sb.AppendLine("[Custom]");
        sb.AppendLine("Url=http://127.0.0.1:5588/translate");
        sb.AppendLine("EnableShortDelay=True");
        sb.AppendLine("DisableSpamChecks=True");
        sb.AppendLine();
        sb.AppendLine("[General]");
        sb.AppendLine($"Language={_appConfig.TargetLanguage}");
        sb.AppendLine($"FromLanguage={_appConfig.SourceLanguage}");
        sb.AppendLine("AutoTranslate=True");
        sb.AppendLine("EnableLogging=False");
        sb.AppendLine("MaxTranslationsPerFrame=10");
        sb.AppendLine();

        sb.AppendLine("[Performance]");
        sb.AppendLine("MaxConcurrentTranslations=100");
        sb.AppendLine();

        sb.AppendLine("[TextFrameworks]");
        sb.AppendLine("EnableUGUI=True");
        sb.AppendLine("EnableTextMeshPro=True");
        sb.AppendLine("EnableNGUI=True");
        sb.AppendLine("EnableIMGUI=True");
        sb.AppendLine("EnableFairyGUI=True");
        sb.AppendLine("EnableUGUIHook=True");
        sb.AppendLine("EnableTextMeshProHook=True");
        sb.AppendLine("EnableNGUIHook=True");
        sb.AppendLine("EnableOnGUIHook=True");
        sb.AppendLine();

        sb.AppendLine("[Behaviour]");
        sb.AppendLine("EnableSilentMode=True");
        sb.AppendLine("ForceMonoModHooks=False");
        sb.AppendLine("EnableTextPathLogging=False");
        sb.AppendLine("IgnoreWhitespaceInDialogue=True");
        sb.AppendLine("MaxCharactersPerTranslation=500");
        sb.AppendLine("RescanUIOnSceneLoad=True");
        sb.AppendLine("ForceRefreshStaticUI=True");
        sb.AppendLine("DisableTextCaching=True");
        sb.AppendLine();

        sb.AppendLine("[ILRepack]");
        sb.AppendLine("HookAllStringLiterals=True");
        sb.AppendLine("UseAdvancedILHook=False");
        sb.AppendLine();

        sb.AppendLine("[Debug]");
        sb.AppendLine("EnableConsoleLogging=False");
        sb.AppendLine();

        sb.AppendLine("[Formatting]");
        sb.AppendLine("WrapText=True");
        sb.AppendLine("MaxTextLength=1000");
        sb.AppendLine();

        sb.AppendLine("[UIControls]");
        sb.AppendLine("FontSize=0");
        sb.AppendLine("EnableFontFallback=True");
        sb.AppendLine();

        sb.AppendLine("[FontOverride]");
        sb.AppendLine("Enable=True");
        sb.AppendLine("OverrideFontName=Microsoft YaHei");
        sb.AppendLine("OverrideFontTextMeshPro=ArialUnicodeSDF");
        sb.AppendLine();

        return sb.ToString();
    }
}
