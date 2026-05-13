using OneClickChineseMod.Core.Providers;

namespace OneClickChineseMod.Tests;

public class ErnieProviderTests
{
    [Fact]
    public async Task TranslateBatchAsync_ReturnsFailoverResult()
    {
        var provider = new ErnieProvider("fake-api-key", "fake-secret-key");

        var result = await provider.TranslateBatchAsync(
            new ProviderBatchTranslationRequest
            {
                Items = new List<ProviderBatchItem>
                {
                    new() { Id = 1, Text = "test" }
                },
                SourceLanguage = "ja",
                TargetLanguage = "zh"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.ShouldFailover);
        Assert.False(result.ShouldRetry);
        Assert.Contains("不支持批量翻译", result.ErrorMessage);

        provider.Dispose();
    }

    [Fact]
    public async Task HealthCheckAsync_WithEmptyApiKey_ReturnsFalse()
    {
        var provider = new ErnieProvider("", "");

        var result = await provider.HealthCheckAsync(CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains("未配置", result.Message);

        provider.Dispose();
    }

    [Fact]
    public void ProviderName_ReturnsBaiduErnie()
    {
        var provider = new ErnieProvider("api", "secret");

        Assert.Equal("百度文心一言", provider.ProviderName);

        provider.Dispose();
    }

    [Fact]
    public void ModelDisplayName_IncludesModelName()
    {
        var provider1 = new ErnieProvider("api", "secret", "ernie-4.5-turbo-8k");
        var provider2 = new ErnieProvider("api", "secret", "ernie-4.0-8k");

        Assert.Contains("ernie-4.5-turbo-8k", provider1.ModelDisplayName);
        Assert.Contains("ernie-4.0-8k", provider2.ModelDisplayName);

        provider1.Dispose();
        provider2.Dispose();
    }

    [Fact]
    public void RpmLimiter_IsInitialized()
    {
        var provider = new ErnieProvider("api", "secret", maxRpm: 500);

        Assert.Equal(500, provider.RpmLimiter.MaxRpm);

        provider.Dispose();
    }
}
