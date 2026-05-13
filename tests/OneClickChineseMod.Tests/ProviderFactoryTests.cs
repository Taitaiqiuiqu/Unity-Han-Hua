using OneClickChineseMod.Core.Providers;
using OneClickChineseMod.Models;

namespace OneClickChineseMod.Tests;

public class ProviderFactoryTests
{
    [Fact]
    public void Create_WithSiliconFlow_ReturnsOpenAICompatibleProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.SiliconFlow,
            ApiKey = "test-key",
            ModelName = "deepseek-ai/DeepSeek-V4-Flash"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("硅基流动", provider.ProviderName);
    }

    [Fact]
    public void Create_WithDeepSeek_ReturnsDeepSeekProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.DeepSeek,
            ApiKey = "test-key",
            ModelName = "deepseek-chat"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("DeepSeek", provider.ProviderName);
    }

    [Fact]
    public void Create_WithQwen_ReturnsOpenAICompatibleProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Qwen,
            ApiKey = "test-key",
            ModelName = "qwen-turbo"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("阿里通义千问", provider.ProviderName);
    }

    [Fact]
    public void Create_WithHunyuan_ReturnsOpenAICompatibleProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Hunyuan,
            ApiKey = "test-key",
            ModelName = "hunyuan-lite"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("腾讯混元", provider.ProviderName);
    }

    [Fact]
    public void Create_WithDoubao_ReturnsOpenAICompatibleProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Doubao,
            ApiKey = "test-key",
            ModelName = "doubao-lite-32k"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("字节豆包", provider.ProviderName);
    }

    [Fact]
    public void Create_WithErnie_ReturnsErnieProvider()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Ernie,
            ApiKey = "test-key",
            SecretKey = "test-secret",
            ModelName = "ernie-4.5-turbo-8k"
        };

        using var provider = ProviderFactory.Create(config);

        Assert.Equal("百度文心一言", provider.ProviderName);
    }

    [Fact]
    public void Create_WithUnsupportedProvider_ThrowsArgumentException()
    {
        var config = new ProviderConfig
        {
            Provider = (ModelProvider)999,
            ApiKey = "test-key"
        };

        var exception = Assert.Throws<ArgumentException>(() => ProviderFactory.Create(config));

        Assert.Contains("不支持", exception.Message);
    }

    [Fact]
    public void GetAvailableModels_SiliconFlow_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.SiliconFlow);

        Assert.Contains("deepseek-ai/DeepSeek-V4-Flash", models);
        Assert.Contains("deepseek-ai/DeepSeek-V4-Pro", models);
    }

    [Fact]
    public void GetAvailableModels_DeepSeek_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.DeepSeek);

        Assert.Contains("deepseek-chat", models);
        Assert.Contains("deepseek-reasoner", models);
    }

    [Fact]
    public void GetAvailableModels_Qwen_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.Qwen);

        Assert.Contains("qwen-turbo", models);
        Assert.Contains("qwen-plus", models);
        Assert.Contains("qwen-max", models);
    }

    [Fact]
    public void GetAvailableModels_Hunyuan_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.Hunyuan);

        Assert.Contains("hunyuan-lite", models);
        Assert.Contains("hunyuan-standard", models);
    }

    [Fact]
    public void GetAvailableModels_Doubao_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.Doubao);

        Assert.Contains("doubao-lite-32k", models);
        Assert.Contains("doubao-pro-32k", models);
        Assert.Contains("doubao-pro-256k", models);
    }

    [Fact]
    public void GetAvailableModels_Ernie_ReturnsExpectedModels()
    {
        var models = ProviderFactory.GetAvailableModels(ModelProvider.Ernie);

        Assert.Contains("ernie-4.5-turbo-8k", models);
        Assert.Contains("ernie-4.0-8k", models);
        Assert.Contains("ernie-speed-8k", models);
        Assert.Contains("ernie-lite-8k", models);
    }

    [Fact]
    public void GetAvailableModels_UnknownProvider_ReturnsEmptyList()
    {
        var models = ProviderFactory.GetAvailableModels((ModelProvider)999);

        Assert.Empty(models);
    }

    [Fact]
    public void GetAllProviders_ReturnsAllEnumValues()
    {
        var providers = ProviderFactory.GetAllProviders();

        Assert.Contains(ModelProvider.SiliconFlow, providers);
        Assert.Contains(ModelProvider.DeepSeek, providers);
        Assert.Contains(ModelProvider.Qwen, providers);
        Assert.Contains(ModelProvider.Hunyuan, providers);
        Assert.Contains(ModelProvider.Doubao, providers);
        Assert.Contains(ModelProvider.Ernie, providers);
    }

    [Theory]
    [InlineData(ModelProvider.Ernie, true)]
    [InlineData(ModelProvider.SiliconFlow, false)]
    [InlineData(ModelProvider.DeepSeek, false)]
    [InlineData(ModelProvider.Qwen, false)]
    [InlineData(ModelProvider.Hunyuan, false)]
    [InlineData(ModelProvider.Doubao, false)]
    public void RequiresSecretKey_ReturnsCorrectValue(ModelProvider provider, bool expected)
    {
        var result = ProviderFactory.RequiresSecretKey(provider);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ModelProvider.SiliconFlow, "API Key")]
    [InlineData(ModelProvider.Ernie, "API Key (Client ID)")]
    [InlineData(ModelProvider.DeepSeek, "API Key")]
    public void GetApiKeyLabel_ReturnsCorrectLabel(ModelProvider provider, string expected)
    {
        var result = ProviderFactory.GetApiKeyLabel(provider);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetApiKeyHint_SiliconFlow_ReturnsSiliconFlowUrl()
    {
        var result = ProviderFactory.GetApiKeyHint(ModelProvider.SiliconFlow);

        Assert.Contains("siliconflow", result);
    }

    [Fact]
    public void GetApiKeyHint_Ernie_ReturnsBaiduUrl()
    {
        var result = ProviderFactory.GetApiKeyHint(ModelProvider.Ernie);

        Assert.Contains("baidu", result);
    }

    [Fact]
    public void Create_SetsCorrectMaxRpm()
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.DeepSeek,
            ApiKey = "test-key",
            ModelName = "deepseek-chat"
        };

        using var provider = ProviderFactory.Create(config, 500);

        Assert.Equal(500, provider.RpmLimiter.MaxRpm);
    }
}
