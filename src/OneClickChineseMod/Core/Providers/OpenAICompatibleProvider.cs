using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core.Providers;

public class OpenAICompatibleProvider : IModelProvider
{
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _providerName;
    private readonly HttpClient _httpClient;

    public string ProviderName => _providerName;
    public string ModelDisplayName => $"{_providerName} ({_model})";
    public RpmLimiter RpmLimiter { get; }

    public OpenAICompatibleProvider(string providerName, string baseUrl, string apiKey, string model, int maxRpm = 800)
    {
        _providerName = providerName;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
        RpmLimiter = new RpmLimiter(maxRpm);
        DiagnosticLogger.Log($"[OpenAICompatProvider Init] Provider: {providerName}, BaseUrl: {_baseUrl}, API Key长度: {(string.IsNullOrEmpty(apiKey) ? "空" : apiKey.Length.ToString())}, Model: {model}, MaxRpm: {maxRpm}");
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version20
        };
    }

    public async Task<ProviderTranslationResult> TranslateAsync(ProviderTranslationRequest request, CancellationToken ct)
    {
        // 保守预估：中日文翻译输出约为源文本 3 倍 token，最少 256，最多 4096。
        // 原 1024 上限会截断 >400 字的游戏对白，导致返回不完整译文。
        var estimatedTokens = Math.Clamp(request.SourceText.Length * 3, 256, 4096);

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = TranslationHelper.BuildSystemPrompt(request) },
                new { role = "user", content = $"<text>{request.SourceText}</text>" }
            },
            temperature = request.Temperature,
            max_tokens = estimatedTokens
        };

        try
        {
            var jsonBody = JsonSerializer.Serialize(payload);
            var url = $"{_baseUrl}/chat/completions";
            DiagnosticLogger.Log($"[{_providerName} TranslateAsync Request] URL: {url}, Model: {_model}");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            DiagnosticLogger.Log($"[{_providerName} TranslateAsync Response] Status: {(int)response.StatusCode}, Body前200字符: {TranslationHelper.Truncate(responseBody, 200)}");

            HookLogger.LogModelRawResponse(request.SourceText, responseBody, response.IsSuccessStatusCode);

            if (response.IsSuccessStatusCode)
            {
                var translatedText = TranslationHelper.ParseOpenAIResponse(responseBody);
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

            return new ProviderTranslationResult
            {
                Success = false,
                ErrorMessage = $"{ProviderName} API {(int)response.StatusCode}: {TranslationHelper.Truncate(responseBody, 200)}",
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
                ShouldFailover = true
            };
        }
    }

    public async Task<ProviderBatchTranslationResult> TranslateBatchAsync(ProviderBatchTranslationRequest request, CancellationToken ct)
    {
        var batchRequestItems = request.Items.Select(i => new { id = i.Id, text = i.Text }).ToList();
        var batchJson = JsonSerializer.Serialize(batchRequestItems, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var systemPrompt = TranslationHelper.BuildBatchSystemPrompt(request.SourceLanguage, request.TargetLanguage);

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"<batch>{batchJson}</batch>" }
            },
            temperature = 0,
            max_tokens = request.MaxTokens
        };

        try
        {
            var jsonBody = JsonSerializer.Serialize(payload);
            var url = $"{_baseUrl}/chat/completions";
            DiagnosticLogger.Log($"[{_providerName} TranslateBatchAsync Request] Items: {request.Items.Count}");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            DiagnosticLogger.Log($"[{_providerName} TranslateBatchAsync Response] Status: {(int)response.StatusCode}");

            HookLogger.LogModelRawResponse(batchJson, responseBody, response.IsSuccessStatusCode);

            if (response.IsSuccessStatusCode)
            {
                var batchResult = TranslationHelper.ParseBatchArrayResponse(responseBody, request.Items);
                batchResult.HttpStatusCode = (int)response.StatusCode;
                batchResult.ProviderName = ProviderName;
                return batchResult;
            }

            var shouldRetry = (int)response.StatusCode == 429;
            var shouldFailover = (int)response.StatusCode is 401 or 403 or >= 500;

            return new ProviderBatchTranslationResult
            {
                Success = false,
                ErrorMessage = $"{ProviderName} API {(int)response.StatusCode}: {TranslationHelper.Truncate(responseBody, 200)}",
                HttpStatusCode = (int)response.StatusCode,
                ProviderName = ProviderName,
                ShouldRetry = shouldRetry,
                ShouldFailover = shouldFailover
            };
        }
        catch (TaskCanceledException)
        {
            return new ProviderBatchTranslationResult
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
            return new ProviderBatchTranslationResult
            {
                Success = false,
                ErrorMessage = $"网络错误: {ex.Message}",
                ProviderName = ProviderName,
                ShouldRetry = true,
                ShouldFailover = true
            };
        }
    }

    public async Task<ProviderHealthCheckResult> HealthCheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            DiagnosticLogger.Log($"[{_providerName} HealthCheck] API Key为空");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "API Key 未配置", ProviderName = ProviderName };
        }

        try
        {
            DiagnosticLogger.Log($"[{_providerName} HealthCheck] 测试连接: {_baseUrl}/chat/completions");
            var payload = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(payload);
            var url = $"{_baseUrl}/chat/completions";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                DiagnosticLogger.Log($"[{_providerName} HealthCheck] 成功! Model: {_model}");
                return new ProviderHealthCheckResult { IsHealthy = true, Message = $"连接成功！模型: {_model}", ProviderName = ProviderName };
            }

            var statusCode = (int)response.StatusCode;
            DiagnosticLogger.Log($"[{_providerName} HealthCheck] 失败! StatusCode: {statusCode}");
            var msg = statusCode switch
            {
                401 => "认证失败: API Key 无效或已过期",
                403 => "访问被拒: 请检查账户余额或权限",
                429 => "请求频率限制: 请稍后重试",
                _ => $"API 错误 ({statusCode})"
            };
            return new ProviderHealthCheckResult { IsHealthy = false, Message = msg, ProviderName = ProviderName };
        }
        catch (TaskCanceledException)
        {
            DiagnosticLogger.Log($"[{_providerName} HealthCheck] 连接超时");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "连接超时", ProviderName = ProviderName };
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"[{_providerName} HealthCheck] 异常: {ex.Message}");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = $"连接失败: {ex.Message}", ProviderName = ProviderName };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
