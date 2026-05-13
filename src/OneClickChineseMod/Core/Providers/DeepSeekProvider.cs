using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core.Providers;

public class DeepSeekProvider : IModelProvider
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;

    private const string BASE_URL = "https://api.deepseek.com/v1/chat/completions";

    private static readonly JsonSerializerOptions BatchJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly Dictionary<string, string> ModelNames = new()
    {
        ["deepseek-chat"] = "DeepSeek-V4-Flash",
        ["deepseek-reasoner"] = "DeepSeek-V4-Pro"
    };

    public string ProviderName => "DeepSeek";
    public string ModelDisplayName => _model == "deepseek-reasoner" ? "DeepSeek-V4-Pro" : "DeepSeek-V4-Flash";
    public RpmLimiter RpmLimiter { get; }

    public DeepSeekProvider(string apiKey, string model = "deepseek-chat", int maxRpm = 800)
    {
        _apiKey = apiKey;
        _model = model;
        RpmLimiter = new RpmLimiter(maxRpm);
        DiagnosticLogger.Log($"[DeepSeekProvider Init] API Key长度: {(string.IsNullOrEmpty(apiKey) ? "空" : apiKey.Length.ToString())}, Model: {model}, MaxRpm: {maxRpm}");
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
            max_tokens = estimatedTokens,
            top_p = 1.0,
            frequency_penalty = 0
        };

        try
        {
            var jsonBody = JsonSerializer.Serialize(payload);
            DiagnosticLogger.Log($"[TranslateAsync Request] URL: {BASE_URL}, Model: {_model}, Text长度: {request.SourceText.Length}, Token预估: {estimatedTokens}");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, BASE_URL);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            DiagnosticLogger.Log($"[TranslateAsync Response] Status: {(int)response.StatusCode}, Body长度: {responseBody.Length}, Body前200字符: {TranslationHelper.Truncate(responseBody, 200)}");

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
                ErrorMessage = $"DeepSeek API {(int)response.StatusCode}: {TranslationHelper.Truncate(responseBody, 200)}",
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
        var batchJson = JsonSerializer.Serialize(batchRequestItems, BatchJsonOptions);

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
            DiagnosticLogger.Log($"[TranslateBatchAsync Request] Items: {request.Items.Count}, MaxTokens: {request.MaxTokens}");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, BASE_URL);
            httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            DiagnosticLogger.Log($"[TranslateBatchAsync Response] Status: {(int)response.StatusCode}, Body长度: {responseBody.Length}");
            DiagnosticLogger.Log($"[TranslateBatchAsync Body] {TranslationHelper.Truncate(responseBody, 500)}");

            HookLogger.LogModelRawResponse(batchJson, responseBody, response.IsSuccessStatusCode);

            if (response.IsSuccessStatusCode)
            {
                var batchResult = TranslationHelper.ParseBatchArrayResponse(responseBody, request.Items);
                batchResult.HttpStatusCode = (int)response.StatusCode;
                batchResult.ProviderName = ProviderName;

                DiagnosticLogger.Log($"[TranslateBatchAsync Parse] 解析结果数量: {batchResult.TranslatedTexts.Count}, 成功: {batchResult.TranslatedTexts.Count - batchResult.FallbackItemIds.Count}, 兜底: {batchResult.FallbackItemIds.Count}/{batchResult.TotalRequested}");
                return batchResult;
            }

            var shouldRetry = (int)response.StatusCode == 429;
            var shouldFailover = (int)response.StatusCode is 401 or 403 or >= 500;

            return new ProviderBatchTranslationResult
            {
                Success = false,
                ErrorMessage = $"DeepSeek API {(int)response.StatusCode}: {TranslationHelper.Truncate(responseBody, 200)}",
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
            DiagnosticLogger.Log("[HealthCheck] API Key为空");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "API Key 未配置", ProviderName = ProviderName };
        }

        try
        {
            DiagnosticLogger.Log($"[HealthCheck] 发送测试请求到 {BASE_URL}");

            var payload = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, BASE_URL);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                var content = TranslationHelper.ParseOpenAIResponse(body);
                var modelInfo = ParseModelFromResponse(body) ?? ModelNames.GetValueOrDefault(_model, _model);
                DiagnosticLogger.Log($"[HealthCheck] 成功! Model: {modelInfo}");
                return new ProviderHealthCheckResult { IsHealthy = true, Message = $"连接成功！模型: {modelInfo}", ProviderName = ProviderName };
            }

            var statusCode = (int)response.StatusCode;
            DiagnosticLogger.Log($"[HealthCheck] 失败! StatusCode: {statusCode}");
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
            DiagnosticLogger.Log("[HealthCheck] 连接超时");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = "连接超时 (15秒)", ProviderName = ProviderName };
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"[HealthCheck] 异常: {ex.Message}");
            return new ProviderHealthCheckResult { IsHealthy = false, Message = $"连接失败: {ex.Message}", ProviderName = ProviderName };
        }
    }

    private static string? ParseModelFromResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("model", out var m))
                return m.GetString();
        }
        catch { }
        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
