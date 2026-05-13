using OneClickChineseMod.Models;

namespace OneClickChineseMod.Tests;

public class ProviderConfigTests
{
    [Fact]
    public void GetDisplayName_SiliconFlow_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.SiliconFlow };

        Assert.Equal("硅基流动", config.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_DeepSeek_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.DeepSeek };

        Assert.Equal("DeepSeek", config.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_Qwen_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.Qwen };

        Assert.Equal("阿里通义千问", config.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_Hunyuan_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.Hunyuan };

        Assert.Equal("腾讯混元", config.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_Doubao_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.Doubao };

        Assert.Equal("字节豆包", config.GetDisplayName());
    }

    [Fact]
    public void GetDisplayName_Ernie_ReturnsCorrectName()
    {
        var config = new ProviderConfig { Provider = ModelProvider.Ernie };

        Assert.Equal("百度文心一言", config.GetDisplayName());
    }

    [Theory]
    [InlineData("deepseek-ai/DeepSeek-V4-Flash", "DeepSeek-V4-Flash (快速便宜)")]
    [InlineData("deepseek-ai/DeepSeek-V4-Pro", "DeepSeek-V4-Pro (精准推理)")]
    [InlineData("unknown-model", "unknown-model")]
    public void GetModelDisplayName_SiliconFlow_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.SiliconFlow,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Theory]
    [InlineData("deepseek-reasoner", "V4-Pro (精准推理)")]
    [InlineData("deepseek-chat", "V4-Flash (快速便宜)")]
    [InlineData("unknown", "V4-Flash (快速便宜)")]
    public void GetModelDisplayName_DeepSeek_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.DeepSeek,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Theory]
    [InlineData("qwen-turbo", "Turbo (快速便宜)")]
    [InlineData("qwen-plus", "Plus (均衡)")]
    [InlineData("qwen-max", "Max (旗舰)")]
    [InlineData("unknown", "unknown")]
    public void GetModelDisplayName_Qwen_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Qwen,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Theory]
    [InlineData("hunyuan-lite", "Lite (免费)")]
    [InlineData("hunyuan-standard", "Standard (标准)")]
    [InlineData("hunyuan-pro", "Pro (旗舰)")]
    [InlineData("hunyuan-turbos-latest", "Turbos (最新)")]
    [InlineData("unknown", "unknown")]
    public void GetModelDisplayName_Hunyuan_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Hunyuan,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Theory]
    [InlineData("doubao-lite-32k", "Lite (快速便宜)")]
    [InlineData("doubao-pro-32k", "Pro (均衡)")]
    [InlineData("doubao-pro-256k", "Pro-256K (长文本)")]
    [InlineData("unknown", "unknown")]
    public void GetModelDisplayName_Doubao_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Doubao,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Theory]
    [InlineData("ernie-4.5-turbo-8k", "4.5-Turbo (快速便宜)")]
    [InlineData("ernie-4.0-8k", "4.0 (旗舰)")]
    [InlineData("ernie-speed-8k", "Speed (极速)")]
    [InlineData("ernie-lite-8k", "Lite (轻量)")]
    [InlineData("unknown", "unknown")]
    public void GetModelDisplayName_Ernie_ReturnsCorrectDisplay(string modelName, string expected)
    {
        var config = new ProviderConfig
        {
            Provider = ModelProvider.Ernie,
            ModelName = modelName
        };

        Assert.Equal(expected, config.GetModelDisplayName());
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new ProviderConfig();

        Assert.Equal(ModelProvider.SiliconFlow, config.Provider);
        Assert.Equal("", config.ApiKey);
        Assert.Null(config.SecretKey);
        Assert.Equal("deepseek-ai/DeepSeek-V4-Flash", config.ModelName);
        Assert.True(config.IsPrimary);
    }

    [Fact]
    public void SecretKey_CanBeSet()
    {
        var config = new ProviderConfig
        {
            SecretKey = "my-secret-key"
        };

        Assert.Equal("my-secret-key", config.SecretKey);
    }
}
