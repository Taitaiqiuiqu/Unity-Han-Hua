using OneClickChineseMod.Models;

namespace OneClickChineseMod.Core.Providers;

public static class ProviderFactory
{
    public static IModelProvider Create(ProviderConfig config, int maxRpm = 800)
    {
        return config.Provider switch
        {
            ModelProvider.SiliconFlow => new OpenAICompatibleProvider(
                "硅基流动",
                "https://api.siliconflow.cn/v1",
                config.ApiKey,
                config.ModelName,
                maxRpm),
            ModelProvider.DeepSeek => new DeepSeekProvider(config.ApiKey, config.ModelName, maxRpm),
            ModelProvider.Qwen => new OpenAICompatibleProvider(
                "阿里通义千问",
                "https://dashscope.aliyuncs.com/compatible-mode/v1",
                config.ApiKey,
                config.ModelName,
                maxRpm),
            ModelProvider.Hunyuan => new OpenAICompatibleProvider(
                "腾讯混元",
                "https://api.hunyuan.cloud.tencent.com/v1",
                config.ApiKey,
                config.ModelName,
                maxRpm),
            ModelProvider.Doubao => new OpenAICompatibleProvider(
                "字节豆包",
                "https://ark.cn-beijing.volces.com/api/v3",
                config.ApiKey,
                config.ModelName,
                maxRpm),
            ModelProvider.Ernie => new ErnieProvider(config.ApiKey, config.SecretKey ?? "", config.ModelName, maxRpm),
            _ => throw new ArgumentException($"不支持的厂商: {config.Provider}")
        };
    }

    public static List<string> GetAvailableModels(ModelProvider provider)
    {
        return provider switch
        {
            ModelProvider.SiliconFlow => new List<string> { "deepseek-ai/DeepSeek-V4-Flash", "deepseek-ai/DeepSeek-V4-Pro" },
            ModelProvider.DeepSeek => new List<string> { "deepseek-chat", "deepseek-reasoner" },
            ModelProvider.Qwen => new List<string> { "qwen-turbo", "qwen-plus", "qwen-max" },
            ModelProvider.Hunyuan => new List<string> { "hunyuan-lite", "hunyuan-standard", "hunyuan-turbos-latest" },
            ModelProvider.Doubao => new List<string> { "doubao-lite-32k", "doubao-pro-32k", "doubao-pro-256k" },
            ModelProvider.Ernie => new List<string> { "ernie-4.5-turbo-8k", "ernie-4.0-8k", "ernie-speed-8k", "ernie-lite-8k" },
            _ => new List<string>()
        };
    }

    public static List<ModelProvider> GetAllProviders()
    {
        return Enum.GetValues<ModelProvider>().ToList();
    }

    public static bool RequiresSecretKey(ModelProvider provider) => provider == ModelProvider.Ernie;

    public static string GetApiKeyLabel(ModelProvider provider)
    {
        return provider switch
        {
            ModelProvider.SiliconFlow => "API Key",
            ModelProvider.Ernie => "API Key (Client ID)",
            _ => "API Key"
        };
    }

    public static string GetApiKeyHint(ModelProvider provider)
    {
        return provider switch
        {
            ModelProvider.SiliconFlow => "在 cloud.siliconflow.cn 获取",
            ModelProvider.DeepSeek => "在 platform.deepseek.com 获取",
            ModelProvider.Qwen => "在 bailian.console.aliyun.com 获取",
            ModelProvider.Hunyuan => "在 console.cloud.tencent.com/hunyuan 获取",
            ModelProvider.Doubao => "在 console.volcengine.com/ark 获取",
            ModelProvider.Ernie => "在 console.bce.baidu.com/qianfan 获取",
            _ => ""
        };
    }
}
