using System.Text.Json;

namespace OneClickChineseMod.Models;

public enum DeepSeekModel
{
    V4Flash,
    V4Pro
}

public class AppConfig
{
    public string DeepSeekApiKey { get; set; } = string.Empty;
    public DeepSeekModel DeepSeekModel { get; set; } = DeepSeekModel.V4Flash;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OneClickChineseMod");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch { }
    }

    public string GetModelName()
    {
        return DeepSeekModel switch
        {
            DeepSeekModel.V4Flash => "deepseek-chat",
            DeepSeekModel.V4Pro => "deepseek-reasoner",
            _ => "deepseek-chat"
        };
    }
}
