using OneClickChineseMod.Models;

namespace OneClickChineseMod.Tests;

public class AppConfigTests
{
    [Fact]
    public void GetPrimaryProvider_WithEmptyList_ReturnsNull()
    {
        var config = new AppConfig { Providers = new List<ProviderConfig>() };

        var result = config.GetPrimaryProvider();

        Assert.Null(result);
    }

    [Fact]
    public void GetPrimaryProvider_WithOneProvider_ReturnsThatProvider()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test", IsPrimary = true }
            }
        };

        var result = config.GetPrimaryProvider();

        Assert.NotNull(result);
        Assert.Equal(ModelProvider.SiliconFlow, result.Provider);
    }

    [Fact]
    public void GetPrimaryProvider_WithMultipleProviders_ReturnsPrimary()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test1", IsPrimary = false },
                new() { Provider = ModelProvider.DeepSeek, ApiKey = "test2", IsPrimary = true },
                new() { Provider = ModelProvider.Ernie, ApiKey = "test3", IsPrimary = false }
            }
        };

        var result = config.GetPrimaryProvider();

        Assert.NotNull(result);
        Assert.Equal(ModelProvider.DeepSeek, result.Provider);
    }

    [Fact]
    public void GetPrimaryProvider_WithNoPrimary_ReturnsFirst()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test1", IsPrimary = false },
                new() { Provider = ModelProvider.DeepSeek, ApiKey = "test2", IsPrimary = false }
            }
        };

        var result = config.GetPrimaryProvider();

        Assert.NotNull(result);
        Assert.Equal(ModelProvider.SiliconFlow, result.Provider);
    }

    [Fact]
    public void GetFallbackProvider_WithEmptyList_ReturnsNull()
    {
        var config = new AppConfig { Providers = new List<ProviderConfig>() };

        var result = config.GetFallbackProvider();

        Assert.Null(result);
    }

    [Fact]
    public void GetFallbackProvider_WithOneProvider_ReturnsNull()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test", IsPrimary = true }
            }
        };

        var result = config.GetFallbackProvider();

        Assert.Null(result);
    }

    [Fact]
    public void GetFallbackProvider_WithMultipleProviders_ReturnsNonPrimary()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test1", IsPrimary = true },
                new() { Provider = ModelProvider.DeepSeek, ApiKey = "test2", IsPrimary = false }
            }
        };

        var result = config.GetFallbackProvider();

        Assert.NotNull(result);
        Assert.Equal(ModelProvider.DeepSeek, result.Provider);
    }

    [Fact]
    public void GetModelName_WithNoProvider_ReturnsDefault()
    {
        var config = new AppConfig { Providers = new List<ProviderConfig>() };

        var result = config.GetModelName();

        Assert.Equal("deepseek-ai/DeepSeek-V4-Flash", result);
    }

    [Fact]
    public void GetModelName_WithProvider_ReturnsModelName()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.SiliconFlow, ApiKey = "test", ModelName = "custom-model", IsPrimary = true }
            }
        };

        var result = config.GetModelName();

        Assert.Equal("custom-model", result);
    }

    [Fact]
    public void GetPrimaryProviderType_WithNoProvider_ReturnsSiliconFlow()
    {
        var config = new AppConfig { Providers = new List<ProviderConfig>() };

        var result = config.GetPrimaryProviderType();

        Assert.Equal(ModelProvider.SiliconFlow, result);
    }

    [Fact]
    public void GetPrimaryProviderType_WithProvider_ReturnsCorrectType()
    {
        var config = new AppConfig
        {
            Providers = new List<ProviderConfig>
            {
                new() { Provider = ModelProvider.Ernie, ApiKey = "test", IsPrimary = true }
            }
        };

        var result = config.GetPrimaryProviderType();

        Assert.Equal(ModelProvider.Ernie, result);
    }

    [Theory]
    [InlineData("en", 1)]
    [InlineData("ko", 2)]
    [InlineData("ja", 0)]
    [InlineData("unknown", 0)]
    public void SourceLanguageToIndex_ReturnsCorrectIndex(string lang, int expected)
    {
        var result = AppConfig.SourceLanguageToIndex(lang);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, "en")]
    [InlineData(2, "ko")]
    [InlineData(0, "ja")]
    [InlineData(99, "ja")]
    public void IndexToSourceLanguage_ReturnsCorrectLanguage(int index, string expected)
    {
        var result = AppConfig.IndexToSourceLanguage(index);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("zh-Hant", 1)]
    [InlineData("zh-TW", 1)]
    [InlineData("zh", 0)]
    [InlineData("en", 0)]
    public void TargetLanguageToIndex_ReturnsCorrectIndex(string lang, int expected)
    {
        var result = AppConfig.TargetLanguageToIndex(lang);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, "zh-Hant")]
    [InlineData(0, "zh")]
    [InlineData(99, "zh")]
    public void IndexToTargetLanguage_ReturnsCorrectLanguage(int index, string expected)
    {
        var result = AppConfig.IndexToTargetLanguage(index);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        var config = new AppConfig();

        Assert.True(config.BatchModeEnabled);
        Assert.Equal(10, config.BatchSize);
        Assert.Equal(500, config.BatchTimeoutMs);
        Assert.Equal(10, config.MaxConcurrency);
        Assert.Equal(800, config.MaxRpm);
        Assert.Equal("en", config.SourceLanguage);
        Assert.Equal("zh", config.TargetLanguage);
        Assert.Empty(config.Providers);
    }

    [Fact]
    public void DirectDeserialization_DoesNotCallMigrateFromLegacy()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"config_test_{Guid.NewGuid()}.json");

        try
        {
            var legacyJson = """
            {
                "ConfigVersion": 0,
                "DeepSeekApiKey": "legacy-key-from-json",
                "DeepSeekModel": 1,
                "Providers": []
            }
            """;

            File.WriteAllText(tempFile, legacyJson);

            var configJson = File.ReadAllText(tempFile);
            var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(configJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(config);
            Assert.Empty(config.Providers);
            Assert.Equal(0, config.ConfigVersion);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
