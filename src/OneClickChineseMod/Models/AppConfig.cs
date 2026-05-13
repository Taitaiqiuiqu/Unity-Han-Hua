using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Models;

[JsonConverter(typeof(ModelProviderJsonConverter))]
public enum ModelProvider
{
    SiliconFlow = 0,
    DeepSeek = 5,
    Qwen,
    Hunyuan,
    Doubao,
    Ernie
}

public class ModelProviderJsonConverter : JsonConverter<ModelProvider>
{
    public override ModelProvider Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var intValue = reader.GetInt32();
            return intValue switch
            {
                0 => ModelProvider.SiliconFlow,
                5 => ModelProvider.DeepSeek,
                6 => ModelProvider.Qwen,
                7 => ModelProvider.Hunyuan,
                8 => ModelProvider.Doubao,
                9 => ModelProvider.Ernie,
                _ => ModelProvider.SiliconFlow
            };
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var strValue = reader.GetString();
            if (Enum.TryParse<ModelProvider>(strValue, out var result))
                return result;
        }

        return ModelProvider.SiliconFlow;
    }

    public override void Write(Utf8JsonWriter writer, ModelProvider value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class ProviderConfig
{
    public ModelProvider Provider { get; set; } = ModelProvider.SiliconFlow;
    public string ApiKey { get; set; } = "";
    public string? SecretKey { get; set; }
    public string ModelName { get; set; } = "deepseek-ai/DeepSeek-V4-Flash";
    public bool IsPrimary { get; set; } = true;

    public string GetDisplayName()
    {
        return Provider switch
        {
            ModelProvider.SiliconFlow => "硅基流动",
            ModelProvider.DeepSeek => "DeepSeek",
            ModelProvider.Qwen => "阿里通义千问",
            ModelProvider.Hunyuan => "腾讯混元",
            ModelProvider.Doubao => "字节豆包",
            ModelProvider.Ernie => "百度文心一言",
            _ => Provider.ToString()
        };
    }

    public string GetModelDisplayName()
    {
        return Provider switch
        {
            ModelProvider.SiliconFlow => ModelName switch
            {
                "deepseek-ai/DeepSeek-V4-Flash" => "DeepSeek-V4-Flash (快速便宜)",
                "deepseek-ai/DeepSeek-V4-Pro" => "DeepSeek-V4-Pro (精准推理)",
                _ => ModelName
            },
            ModelProvider.DeepSeek => ModelName == "deepseek-reasoner" ? "V4-Pro (精准推理)" : "V4-Flash (快速便宜)",
            ModelProvider.Qwen => ModelName switch
            {
                "qwen-turbo" => "Turbo (快速便宜)",
                "qwen-plus" => "Plus (均衡)",
                "qwen-max" => "Max (旗舰)",
                _ => ModelName
            },
            ModelProvider.Hunyuan => ModelName switch
            {
                "hunyuan-lite" => "Lite (免费)",
                "hunyuan-standard" => "Standard (标准)",
                "hunyuan-pro" => "Pro (旗舰)",
                "hunyuan-turbos-latest" => "Turbos (最新)",
                _ => ModelName
            },
            ModelProvider.Doubao => ModelName switch
            {
                "doubao-lite-32k" => "Lite (快速便宜)",
                "doubao-pro-32k" => "Pro (均衡)",
                "doubao-pro-256k" => "Pro-256K (长文本)",
                _ => ModelName
            },
            ModelProvider.Ernie => ModelName switch
            {
                "ernie-4.5-turbo-8k" => "4.5-Turbo (快速便宜)",
                "ernie-4.0-8k" => "4.0 (旗舰)",
                "ernie-speed-8k" => "Speed (极速)",
                "ernie-lite-8k" => "Lite (轻量)",
                _ => ModelName
            },
            _ => ModelName
        };
    }
}

public class AppConfig
{
    public int ConfigVersion { get; set; } = 0;

    [Obsolete("使用 Providers 列表代替，仅用于旧配置迁移")]
    public string DeepSeekApiKey { get; set; } = string.Empty;
    [Obsolete("使用 Providers 列表代替，仅用于旧配置迁移")]
    public int DeepSeekModel { get; set; } = 0;
    public int MaxConcurrency { get; set; } = 10;
    public int MaxRpm { get; set; } = 800;

    public bool BatchModeEnabled { get; set; } = true;
    public int BatchSize { get; set; } = 10;
    public int BatchTimeoutMs { get; set; } = 150;

    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "zh";

    public List<ProviderConfig> Providers { get; set; } = new();

    private const int CURRENT_CONFIG_VERSION = 1;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SenGameLoc");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ModelProviderJsonConverter() }
    };

    private static readonly string[] SourceLanguages = { "ja", "en", "ko" };
    private static readonly string[] TargetLanguages = { "zh", "zh-Hant" };

    public static int SourceLanguageToIndex(string lang)
    {
        var idx = Array.IndexOf(SourceLanguages, lang);
        return idx >= 0 ? idx : 1; // 默认 en
    }

    public static string IndexToSourceLanguage(int index)
    {
        return index >= 0 && index < SourceLanguages.Length ? SourceLanguages[index] : "en";
    }

    public static int TargetLanguageToIndex(string lang)
    {
        var idx = Array.IndexOf(TargetLanguages, lang);
        return idx >= 0 ? idx : 0; // 默认 zh
    }

    public static string IndexToTargetLanguage(int index)
    {
        return index >= 0 && index < TargetLanguages.Length ? TargetLanguages[index] : "zh";
    }

    public ProviderConfig? GetPrimaryProvider()
    {
        return Providers.FirstOrDefault(p => p.IsPrimary);
    }

    public ProviderConfig? GetFallbackProvider()
    {
        return Providers.FirstOrDefault(p => !p.IsPrimary);
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                config.MigrateFromLegacy();
                return config;
            }
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"加载配置失败: {ex.Message}，使用默认配置");
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            ConfigVersion = CURRENT_CONFIG_VERSION;

            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"保存配置失败: {ex.Message}");
        }
    }

    private void MigrateFromLegacy()
    {
        if (ConfigVersion >= CURRENT_CONFIG_VERSION)
            return;

        if (Providers.Count > 0)
        {
            ConfigVersion = CURRENT_CONFIG_VERSION;
            return;
        }

        // 从旧版单 Provider 配置迁移
        if (!string.IsNullOrEmpty(DeepSeekApiKey))
        {
            Providers.Add(new ProviderConfig
            {
                Provider = ModelProvider.DeepSeek,
                ApiKey = DeepSeekApiKey,
                ModelName = DeepSeekModel == 0 ? "deepseek-chat" : "deepseek-reasoner",
                IsPrimary = true
            });
        }

        ConfigVersion = CURRENT_CONFIG_VERSION;
    }
}
