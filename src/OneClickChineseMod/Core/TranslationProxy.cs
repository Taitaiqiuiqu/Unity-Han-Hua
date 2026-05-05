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

                while (true)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrEmpty(line) || line == "\r") break;
                }

                if (httpMethod != "GET")
                {
                    await WriteHttpResponse(stream, 405, "Method Not Allowed", "");
                    return;
                }

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
                    await WriteHttpResponse(stream, 200, "OK", result);
                }
                else
                {
                    FailCount++;
                    await WriteHttpResponse(stream, 500, "Internal Server Error", "");
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
