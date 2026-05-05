using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public class TranslationProxy : IDisposable
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly CancellationTokenSource _cts;
    private TcpListener? _listener;
    private Task? _listenTask;

    private const int DEFAULT_PORT = 5588;
    private const string DEEPSEEK_URL = "https://api.deepseek.com/chat/completions";

    public int Port { get; }
    public bool IsRunning { get; private set; }
    public int TotalRequests { get; private set; }
    public int SuccessCount { get; private set; }
    public int FailCount { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public event Action<string, string, string?>? OnTranslation;
    public event Action<int, int, int>? OnStatisticsChanged;

    public TranslationProxy(string apiKey, string model, int port = DEFAULT_PORT)
    {
        _apiKey = apiKey;
        _model = model;
        Port = port;
        _cts = new CancellationTokenSource();
    }

    public bool Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            IsRunning = true;
            ConsoleUtils.WriteInfo($"翻译代理已启动 (127.0.0.1:{Port})");
            return true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            ConsoleUtils.WriteError($"启动代理失败: {ex.Message}");
            return false;
        }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClient(client, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (IOException) { break; }
            catch (SocketException) { break; }
        }
    }

    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 10000;
                client.SendTimeout = 10000;

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var requestLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                var httpMethod = parts[0];
                var url = parts[1];

                int contentLength = 0;
                string contentType = "";
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrEmpty(line) || line == "\r") break;
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line.Substring("Content-Length:".Length).Trim());
                    if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                        contentType = line.Substring("Content-Type:".Length).Trim();
                }

                if (httpMethod == "GET" && (url == "/health" || url == "/healthz"))
                {
                    await WriteHttpResponse(stream, 200, "OK", "OK");
                }
                else if (httpMethod == "POST")
                {
                    await HandlePostRequest(stream, reader, url, contentLength, contentType, ct);
                }
                else if (httpMethod == "GET")
                {
                    await HandleGetRequest(stream, url, ct);
                }
                else
                {
                    await WriteHttpResponse(stream, 405, "Method Not Allowed", "");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            ConsoleUtils.WriteDebug($"代理处理异常: {ex.Message}");
        }
    }

    private async Task HandlePostRequest(NetworkStream stream, StreamReader reader, string url, int contentLength, string contentType, CancellationToken ct)
    {
        if (!url.StartsWith("/translate"))
        {
            ConsoleUtils.WriteDebug($"未知路由 POST {url} → 404");
            FailCount++;
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 404, "Not Found", "");
            return;
        }

        if (contentLength <= 0 || contentLength > 256 * 1024)
        {
            await WriteHttpResponse(stream, 400, "Bad Request", "");
            return;
        }

        var bodyBuffer = new char[contentLength];
        await reader.ReadBlockAsync(bodyBuffer, 0, contentLength);
        var body = new string(bodyBuffer);

        var sourceText = ExtractSourceText(body, contentType);

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            ConsoleUtils.WriteDebug($"无法从请求体提取文本 ({contentLength}B, Content-Type={contentType}): {body[..Math.Min(body.Length, 200)]}");
            FailCount++;
            OnTranslation?.Invoke("", "", "请求体无法解析");
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 400, "Bad Request", "");
            return;
        }

        TotalRequests++;
        var preview = sourceText.Length > 50 ? sourceText[..50] + "..." : sourceText;
        ConsoleUtils.WriteDebug($"翻译请求 [{TotalRequests}]: {preview}");

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional translator. Translate the following text to Chinese. Return ONLY the translated text, no explanations, no notes, no quotation marks. Keep all special characters, newlines, and formatting exactly as they appear in the original." },
                    new { role = "user", content = sourceText }
                },
                temperature = 0.3,
                max_tokens = 4096
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, DEEPSEEK_URL);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                SuccessCount++;
                string translatedText = "";
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    translatedText = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                    translatedText = translatedText.Trim();
                }
                catch { }

                if (string.IsNullOrEmpty(translatedText))
                {
                    translatedText = sourceText;
                }

                OnTranslation?.Invoke(sourceText, translatedText, null);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 200, "OK", translatedText);
            }
            else
            {
                FailCount++;
                var statusCode = (int)response.StatusCode;
                LastError = $"DeepSeek API {statusCode}";
                ConsoleUtils.WriteError($"DeepSeek API 错误 ({statusCode}): {responseBody[..Math.Min(responseBody.Length, 300)]}");
                OnTranslation?.Invoke(sourceText, "", LastError);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, statusCode, "Error", "");
            }
        }
        catch (Exception ex)
        {
            FailCount++;
            LastError = ex.Message;
            ConsoleUtils.WriteError($"代理转发异常: {ex.Message}");
            OnTranslation?.Invoke(sourceText, "", ex.Message);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 500, "Proxy Error", "");
        }
    }

    private static string ExtractSourceText(string body, string contentType)
    {
        bool isJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || body.StartsWith("{");
        bool isFormUrlEncoded = contentType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

        if (isJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("text", out var textProp))
                    return textProp.GetString() ?? "";

                if (root.TryGetProperty("content", out var contentProp))
                    return contentProp.GetString() ?? "";

                if (root.TryGetProperty("messages", out var messages))
                {
                    foreach (var msg in messages.EnumerateArray())
                    {
                        if (msg.TryGetProperty("role", out var role) && role.GetString() == "user")
                            return msg.GetProperty("content").GetString() ?? "";
                    }
                }
            }
            catch { }
            return "";
        }

        if (isFormUrlEncoded || (body.Contains("&") && body.Contains("=")))
        {
            foreach (var pair in body.Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq < 0) continue;
                var key = Uri.UnescapeDataString(pair[..eq]);
                if (key.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[(eq + 1)..]);
                }
            }
        }

        return body.Trim();
    }

    private async Task HandleGetRequest(NetworkStream stream, string url, CancellationToken ct)
    {
        int queryStart = url.IndexOf('?');
        if (queryStart < 0 || queryStart >= url.Length - 1)
        {
            await WriteHttpResponse(stream, 400, "Bad Request", "");
            return;
        }

        var query = url[(queryStart + 1)..];
        var text = GetQueryParam(query, "text") ?? GetQueryParam(query, "q") ?? "";

        if (string.IsNullOrWhiteSpace(text))
        {
            await WriteHttpResponse(stream, 400, "Bad Request", "");
            return;
        }

        var sourceLang = GetQueryParam(query, "from") ?? GetQueryParam(query, "source") ?? "ja";
        var targetLang = GetQueryParam(query, "to") ?? GetQueryParam(query, "target") ?? "zh";

        TotalRequests++;
        ConsoleUtils.WriteDebug($"翻译请求 [{TotalRequests}]: {text.Substring(0, Math.Min(text.Length, 40))}...");

        var result = await TranslateWithDeepSeek(text, sourceLang, targetLang, ct);

        if (result != null)
        {
            SuccessCount++;
            OnTranslation?.Invoke(text, result, null);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 200, "OK", result);
        }
        else
        {
            FailCount++;
            OnTranslation?.Invoke(text, "", LastError);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 500, "Proxy Error", "");
        }
    }

    private static string? GetQueryParam(string query, string key)
    {
        foreach (var pair in query.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var k = Uri.UnescapeDataString(pair[..eq]);
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }
        return null;
    }

    private static async Task WriteHttpResponse(NetworkStream stream, int statusCode, string statusText, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                     $"Content-Type: text/plain; charset=utf-8\r\n" +
                     $"Content-Length: {bodyBytes.Length}\r\n" +
                     $"Connection: close\r\n" +
                     $"Access-Control-Allow-Origin: *\r\n" +
                     $"\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(bodyBytes);
    }

    private async Task<string?> TranslateWithDeepSeek(string text, string sourceLang, string targetLang, CancellationToken ct)
    {
        var langNames = new Dictionary<string, string>
        {
            ["ja"] = "Japanese",
            ["zh"] = "Chinese",
            ["en"] = "English",
            ["ko"] = "Korean",
            ["fr"] = "French",
            ["de"] = "German",
            ["es"] = "Spanish"
        };

        var srcName = langNames.TryGetValue(sourceLang, out var s) ? s : sourceLang;
        var tgtName = langNames.TryGetValue(targetLang, out var t) ? t : targetLang;

        var systemPrompt = $"You are a professional translator. Translate the following text from {srcName} to {tgtName}. Return ONLY the translated text, no explanations, no notes, no quotation marks. Keep all special characters, newlines, and formatting exactly as they appear in the original.";

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = text }
            },
            temperature = 0.3,
            max_tokens = 4096
        };

        var json = JsonSerializer.Serialize(payload);

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var request = new HttpRequestMessage(HttpMethod.Post, DEEPSEEK_URL);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            LastError = $"HTTP {(int)response.StatusCode}: {(errorBody.Length > 200 ? errorBody[..200] + "..." : errorBody)}";
            ConsoleUtils.WriteError($"DeepSeek API 错误 ({response.StatusCode}): {errorBody}");
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (content == null) return null;

            content = content.Trim();
            content = Regex.Replace(content, "^[\"'']+|[\"'']+$", "");

            return content;
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"解析 DeepSeek 响应失败: {ex.Message}");
            return null;
        }
    }

    public static async Task<(bool Success, string Message)> TestConnectionAsync(string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "请先填写 API Key");

        try
        {
            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                max_tokens = 10,
                temperature = 0
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var request = new HttpRequestMessage(HttpMethod.Post, DEEPSEEK_URL);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var content = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var modelName = doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() : model;
                        return (true, $"连接成功！模型: {modelName}");
                    }
                }
                catch
                {
                    return (true, "连接成功！(响应解析异常但API可达)");
                }

                return (true, "连接成功！");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var statusCode = (int)response.StatusCode;

                if (statusCode == 401)
                    return (false, $"认证失败 (401): API Key 无效或已过期");
                if (statusCode == 403)
                    return (false, $"访问被拒 (403): 请检查账户余额或权限");
                if (statusCode == 429)
                    return (false, $"请求频率限制 (429): 请稍后重试");

                var brief = errorBody.Length > 200 ? errorBody[..200] + "..." : errorBody;
                return (false, $"API 错误 ({statusCode}): {brief}");
            }
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时 (15秒): 请检查网络，可能需要代理");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"网络错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"未知错误: {ex.Message}");
        }
    }

    public void Dispose()
    {
        IsRunning = false;
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        _cts.Dispose();
    }
}
