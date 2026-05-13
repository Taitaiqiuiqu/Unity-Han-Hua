using System.Net.Http;

namespace OneClickChineseMod.Core.Providers;

public interface IModelProvider : IDisposable
{
    string ProviderName { get; }
    string ModelDisplayName { get; }
    RpmLimiter RpmLimiter { get; }
    Task<ProviderTranslationResult> TranslateAsync(ProviderTranslationRequest request, CancellationToken ct);
    Task<ProviderBatchTranslationResult> TranslateBatchAsync(ProviderBatchTranslationRequest request, CancellationToken ct);
    Task<ProviderHealthCheckResult> HealthCheckAsync(CancellationToken ct);
}

public class ProviderTranslationRequest
{
    public string SourceText { get; set; } = "";
    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "zh";
    public double Temperature { get; set; } = 0;
    public int MaxTokens { get; set; } = 4096;
}

public class ProviderTranslationResult
{
    public bool Success { get; set; }
    public string TranslatedText { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
    public string ProviderName { get; set; } = "";
    public bool ShouldRetry { get; set; }
    public bool ShouldFailover { get; set; }
}

public class ProviderBatchTranslationRequest
{
    public List<ProviderBatchItem> Items { get; set; } = new();
    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "zh";
    public int MaxTokens { get; set; } = 4096;
}

public class ProviderBatchItem
{
    public long Id { get; init; }
    public string Text { get; init; } = "";
}

public class ProviderBatchTranslationResult
{
    public bool Success { get; set; }
    public Dictionary<long, string> TranslatedTexts { get; set; } = new();
    public HashSet<long> FallbackItemIds { get; set; } = new();
    public int TotalRequested { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
    public string ProviderName { get; set; } = "";
    public bool ShouldRetry { get; set; }
    public bool ShouldFailover { get; set; }
}

public class ProviderHealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = "";
    public string ProviderName { get; set; } = "";
}
