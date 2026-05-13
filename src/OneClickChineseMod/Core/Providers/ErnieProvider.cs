using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core.Providers;

public class ErnieProvider : IModelProvider
{
    private readonly string _apiKey;
    private readonly string _secretKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    private const string TOKEN_URL = "https://aip.baidubce.com/oauth/2.0/token";
    private const string CHAT_URL_TEMPLATE = "https://aip.baidubce.com/rpc/2.0/ai_custom/v1/wenxinworkshop/chat/{0}";

    private static readonly Dictionary<string, string> ModelEndpoints = new()
    {
        ["ernie-4.0-8k"] = "completions_pro",
        ["ernie-4.5-turbo-8k"] = "ernie-4.5-turbo-8k",
        ["ernie-3.5-8k"] = "completions",
        ["ernie-speed-8k"] = "ernie_speed",
        ["ernie-lite-8k"] = "ernie-lite-8k"
    };

    public string ProviderName => "百度文心一言";
    public string ModelDisplayName => $"文心一言 ({_model})";
    public RpmLimiter RpmLimiter { get; }

    public ErnieProvider(string apiKey, string secretKey, string model = "ernie-4.5-turbo-8k", int maxRpm = 800)
    {
        _apiKey = apiKey;
        _secretKey = secretKey;
        _model = model;
        RpmLimiter = new RpmLimiter(maxRpm);
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<ProviderTranslationResult> TranslateAsync(ProviderTranslationRequest request, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);
        if (accessToken == null)
        {
            return new ProviderTranslationResult
            {
                Success = false,
                ErrorMessage = "获取 access_token 失败，请检查 API Key 和 Secret Key",
                ProviderName = ProviderName,
                ShouldRetry = false,
                ShouldFailover = false
            };
        }

        var endpoint = ModelEndpoints.GetValueOrDefault(_model, "completions_pro");
        var url = $"{string.Format(CHAT_URL_TEMPLATE, endpoint)}?access_token={accessToken}";

        var payload = new
        {
            messages = new[]
            {
                new { role = "user", content = $"{TranslationHelper.BuildSystemPrompt(request)}\n\n<text>{request.SourceText}</text>" }
            },
            temperature = request.Temperature,
            top_p = 0.8
        };

        try
        {
            var jsonBody = JsonSerializer.Serialize(payload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            HookLogger.LogModelRawResponse(request.SourceText, responseBody, response.IsSuccessStatusCode);

            if (response.IsSuccessStatusCode)
            {
                var translatedText = ParseErnieResponse(responseBody);
                return new ProviderTranslationResult
                {
                    Success = true,
                    TranslatedText = translatedText,
                    HttpStatusCode = (int)response.StatusCode,
                    ProviderName = ProviderName
                };
            }

            var shouldRetry = (int)response.StatusCode == 429;
            var shouldFailover = (int)response.StatusCode is 401 or 403 or >= 500;

            if ((int)response.StatusCode == 401)
            {
                _cachedAccessToken = null;
                shouldRetry = true;
            }

            return new ProviderTranslationResult
            {
                Success = false,
                ErrorMessage = $"文心一言 API {(int)response.StatusCode}: {Truncate(responseBody, 200)}",
                HttpStatusCode = (int)response.StatusCode,
                ProviderName = ProviderName,
                ShouldRetry = shouldRetry,
                ShouldFailover = shouldFailover
            };
        }
        catch (TaskCanceledException)
        {
            return new ProviderTranslationResult
            {
                Success = false,
                ErrorMessage = "请求超时",
                ProviderName = ProviderName,
                ShouldRetry = true,
                ShouldFailover = false
            };
        }
        catch (HttpRequestException ex)
        {
            return new ProviderTranslationResult
            {
                Success = false,
                ErrorMessage = $"网络错误: {ex.Message}",
                ProviderName = ProviderName,
                ShouldRetry = true,
                ShouldFailover = false
            };
        }
    }

    public Task<ProviderBatchTranslationResult> TranslateBatchAsync(ProviderBatchTranslationRequest request, CancellationToken ct)
    {
        var result = new ProviderBatchTranslationResult
        {
            Success = false,
            ErrorMessage = "百度文心一言 API 不支持批量翻译格式，将通过单条翻译逐条处理",
            ProviderName = ProviderName,
            ShouldRetry = false,
            ShouldFailover = false
        };
        return Task.FromResult(result);
    }

    public async Task<ProviderHealthCheckResult> HealthCheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_secretKey))
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "API Key 或 Secret Key 未配置", ProviderName = ProviderName };

        var tokenResult = await GetAccessTokenAsync(ct);
        if (tokenResult == null)
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "获取 access_token 失败", ProviderName = ProviderName };

        try
        {
            var endpoint = ModelEndpoints.GetValueOrDefault(_model, "completions_pro");
            var url = $"{string.Format(CHAT_URL_TEMPLATE, endpoint)}?access_token={tokenResult}";

            var payload = new
            {
                messages = new[] { new { role = "user", content = "Hi" } },
                max_output_tokens = 5,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new ProviderHealthCheckResult { IsHealthy = true, Message = $"连接成功！模型: {_model}", ProviderName = ProviderName };

            var statusCode = (int)response.StatusCode;
            var msg = statusCode switch
            {
                401 => "认证失败: access_token 无效",
                403 => "访问被拒: 请检查权限或余额",
                429 => "请求频率限制",
                _ => $"API 错误 ({statusCode})"
            };
            return new ProviderHealthCheckResult { IsHealthy = false, Message = msg, ProviderName = ProviderName };
        }
        catch (TaskCanceledException)
        {
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "连接超时", ProviderName = ProviderName };
        }
        catch (Exception ex)
        {
            return new ProviderHealthCheckResult { IsHealthy = false, Message = $"连接失败: {ex.Message}", ProviderName = ProviderName };
        }
    }

    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedAccessToken;

        await _tokenRefreshLock.WaitAsync(ct);
        try
        {
            if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiresAt)
                return _cachedAccessToken;

            // 注意: 百度 OAuth API 要求凭证通过 query string 传递
            // 确保日志中不记录此 URL 的完整形式
            var url = $"{TOKEN_URL}?grant_type=client_credentials&client_id={_apiKey}&client_secret={_secretKey}";
            var response = await _httpClient.PostAsync(url, null, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out var token))
            {
                _cachedAccessToken = token.GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 2592000;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300);
                return _cachedAccessToken;
            }
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"获取百度 access_token 失败: {ex.Message}");
        }
        finally
        {
            _tokenRefreshLock.Release();
        }
        return null;
    }

    private static string ParseErnieResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("result", out var result))
            {
                var content = result.GetString() ?? "";
                return content.Trim();
            }
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteWarning($"解析文心一言响应失败: {ex.Message}");
        }
        return "";
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
