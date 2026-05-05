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

        // 如果配置文件已存在，保留它（避免覆盖用户的自定义配置）
        if (File.Exists(configPath))
        {
            ConsoleUtils.WriteDebug("AutoTranslatorConfig.ini 已存在，保留现有配置");
            return;
        }

        var configContent = GenerateConfigContent();
        File.WriteAllText(configPath, configContent, Encoding.UTF8);
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
        }
        else
        {
            ConsoleUtils.WriteDebug("doorstop_config.ini 已存在 (来自 BepInEx)，跳过生成");
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
        sb.AppendLine("Endpoint=DeepSeekTranslate");
        sb.AppendLine("FallbackEndpoint=");
        sb.AppendLine();

        sb.AppendLine("[General]");
        sb.AppendLine("Language=zh");
        sb.AppendLine("FromLanguage=en");
        sb.AppendLine("AutoTranslate=True");
        sb.AppendLine("EnableLogging=True");
        sb.AppendLine();

        sb.AppendLine("[TextFrameworks]");
        sb.AppendLine("EnableUGUI=True");
        sb.AppendLine("EnableTextMeshPro=True");
        sb.AppendLine("EnableNGUI=True");
        sb.AppendLine("EnableIMGUI=True");
        sb.AppendLine("EnableFairyGUI=True");
        sb.AppendLine();

        sb.AppendLine("[Behaviour]");
        sb.AppendLine("EnableSilentMode=False");
        sb.AppendLine("ForceMonoModHooks=False");
        sb.AppendLine("EnableTextPathLogging=True");
        sb.AppendLine();

        sb.AppendLine("[Debug]");
        sb.AppendLine("EnableConsoleLogging=True");
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

        sb.AppendLine("[BaiduTranslate]");
        sb.AppendLine("Enabled=False");
        sb.AppendLine();

        sb.AppendLine("[GoogleTranslate]");
        sb.AppendLine("Enabled=False");
        sb.AppendLine();

        sb.AppendLine("[DeepSeek]");
        sb.AppendLine("Endpoint=https://api.deepseek.com/chat/completions");
        sb.AppendLine($"ApiKey={_appConfig.DeepSeekApiKey}");
        sb.AppendLine($"Model={_appConfig.GetModelName()}");
        sb.AppendLine("Temperature=1.3");
        sb.AppendLine("MaxTokensMode=Dynamic");
        sb.AppendLine("DynamicMaxTokensMultiplier=1.5");
        sb.AppendLine("AddEndingAssistantPrompt=True");
        sb.AppendLine("SplitByLine=False");
        sb.AppendLine("MaxConcurrency=1");
        sb.AppendLine("BatchTranslate=False");
        sb.AppendLine("MaxTranslationsPerRequest=1");
        sb.AppendLine("CoroutineWaitCountBeforeRead=150");
        sb.AppendLine("MaxRetries=1");
        sb.AppendLine("UseThreadPool=True");
        sb.AppendLine("DisableThinking=True");
        sb.AppendLine("Debug=True");

        return sb.ToString();
    }
}
