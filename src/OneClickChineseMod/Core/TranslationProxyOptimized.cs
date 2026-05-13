using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneClickChineseMod.Core.Providers;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Core;

public class TranslationProxyOptimized : IDisposable
{
    private readonly AppConfig _appConfig;
    private readonly List<IModelProvider> _primaryProviders;
    private readonly IModelProvider? _fallbackProvider;
    private readonly CancellationTokenSource _cts;
    private TcpListener? _listener;
    private Task? _listenTask;

    private readonly SemaphoreSlim _concurrencyLimiter;
    private const int DEFAULT_MAX_CONCURRENCY = 10;

    private readonly ConcurrentDictionary<string, TranslationResult> _resultCache = new();
    private readonly ConcurrentQueue<string> _cacheKeyOrder = new();
    private const int MAX_CACHE_SIZE = 10000;

    private readonly ConcurrentDictionary<string, Task<string?>> _inFlightRequests = new();

    private Task? _cacheCleanerTask;

    private const int DEFAULT_PORT = 5588;
    private const int MAX_RETRY_ATTEMPTS = 2;
    private const int TCP_RECEIVE_TIMEOUT_MS = 30000;
    private const int TCP_SEND_TIMEOUT_MS = 10000;
    private const int MAX_CONTENT_LENGTH = 256 * 1024;
    private const int DEFAULT_MAX_TOKENS = 4096;
    private const int BATCH_SIZE_OVERFLOW_MULTIPLIER = 5;
    private const int KEEP_ALIVE_TIMEOUT_SEC = 30;
    private const int KEEP_ALIVE_MAX_REQUESTS = 100;
    private const int LOG_TRUNCATE_LENGTH = 200;
    private const int RPM_RETRY_ATTEMPTS = 3;
    private const int RPM_RETRY_DELAY_MS = 1000;

    private readonly ConcurrentQueue<BatchItem> _batchQueue = new();
    private readonly SemaphoreSlim _batchSignal = new(0);
    private int _batchSize = 10;
    private long _batchIdCounter;
    private CancellationTokenSource? _batchFlushCts;
    private Task? _batchFlushTask;
    private bool _batchModeEnabled = true;

    private CancellationTokenSource? _batchTimerCts;
    private readonly object _batchTimerLock = new();

    private int _consecutiveSingleItemBatches;
    private const int ADAPTIVE_TIMEOUT_THRESHOLD = 3;
    private const int REDUCED_BATCH_TIMEOUT_MS = 120;

    private readonly string _defaultSourceLanguage;
    private readonly string _defaultTargetLanguage;

    public int Port { get; }
    public bool IsRunning { get; private set; }
    public int CurrentRpm => _primaryProviders.FirstOrDefault()?.RpmLimiter?.GetCurrentRpm() ?? 0;
    private int _totalRequests;
    public int TotalRequests => _totalRequests;
    private int _successCount;
    public int SuccessCount => _successCount;
    private int _failCount;
    public int FailCount => _failCount;
    private int _cacheHits;
    public int CacheHits => _cacheHits;
    private int _cacheMisses;
    public int CacheMisses => _cacheMisses;
    public double CacheHitRate => (CacheHits + CacheMisses) > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;
    public string LastError { get; private set; } = string.Empty;
    public double AverageLatencyMs => _latencyCount > 0 ? (double)_totalLatencyMs / _latencyCount : 0;
    public int LatencySampleCount => _latencyCount;
    private long _totalLatencyMs;
    private int _latencyCount;
    public int BatchSize { get => _batchSize; set => _batchSize = Math.Clamp(value, 2, 50); }
    public bool BatchModeEnabled
    {
        get => _batchModeEnabled;
        set
        {
            _batchModeEnabled = value;
            if (!value) FlushRemainingBatch();
        }
    }
    public int PendingBatchCount => _batchQueue.Count;

    public event Action<string, string, string?>? OnTranslation;
    public event Action<int, int, int>? OnStatisticsChanged;
    public event Action<int, int>? OnRpmChanged;
    public event Action<int>? OnBatchFlushed;

    private readonly System.Timers.Timer _rpmRefreshTimer;

    public TranslationProxyOptimized(AppConfig appConfig, int port = DEFAULT_PORT, int maxConcurrency = DEFAULT_MAX_CONCURRENCY, int maxRpm = 800)
    {
        _appConfig = appConfig;
        Port = port;
        _defaultSourceLanguage = appConfig.SourceLanguage;
        _defaultTargetLanguage = appConfig.TargetLanguage;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _cts = new CancellationTokenSource();

        _batchModeEnabled = appConfig.BatchModeEnabled;
        _batchSize = appConfig.BatchSize;

        DiagnosticLogger.Log($"[TranslationProxy] 初始化: Port={port}, MaxConcurrency={maxConcurrency}, MaxRpm={maxRpm}, BatchEnabled={_batchModeEnabled}, BatchSize={_batchSize}, SourceLang={appConfig.SourceLanguage}, TargetLang={appConfig.TargetLanguage}");

        _rpmRefreshTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _rpmRefreshTimer.Elapsed += (_, _) =>
        {
            var rpm = CurrentRpm;
            OnRpmChanged?.Invoke(rpm, maxRpm);
        };

        _primaryProviders = new List<IModelProvider>();
        var primaryConfig = appConfig.GetPrimaryProvider();
        if (primaryConfig != null)
        {
            DiagnosticLogger.Log($"[TranslationProxy] 创建主服务: {primaryConfig.Provider}, Model: {primaryConfig.ModelName}, IsPrimary: {primaryConfig.IsPrimary}");
            _primaryProviders.Add(ProviderFactory.Create(primaryConfig, maxRpm));
        }

        var fallbackConfig = appConfig.GetFallbackProvider();
        if (fallbackConfig != null)
        {
            DiagnosticLogger.Log($"[TranslationProxy] 创建备用服务: {fallbackConfig.Provider}, Model: {fallbackConfig.ModelName}");
            _fallbackProvider = ProviderFactory.Create(fallbackConfig, maxRpm);
        }
    }

    public bool Start()
    {
        try
        {
            if (_primaryProviders.Count == 0)
            {
                DiagnosticLogger.Log("[TranslationProxy Start] 失败: 未配置任何翻译服务厂商");
                ConsoleUtils.WriteError("未配置任何翻译服务厂商");
                return false;
            }

            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            DiagnosticLogger.Log($"[TranslationProxy Start] TCP监听已启动: 127.0.0.1:{Port}");
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            _cacheCleanerTask = Task.Run(() => CacheCleanerLoop(_cts.Token));
            _rpmRefreshTimer.Start();

            _batchFlushCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _batchFlushTask = Task.Run(() => BatchFlushLoop(_batchFlushCts.Token));

            HookLogger.Initialize();

            IsRunning = true;

            var primaryName = _primaryProviders[0].ModelDisplayName;
            var fallbackName = _fallbackProvider != null ? $", 备用: {_fallbackProvider.ModelDisplayName}" : "";
            DiagnosticLogger.Log($"[TranslationProxy Start] 成功! 主: {primaryName}{fallbackName}, 批量: 启用 (队列{_batchSize}条/滑动窗口)");
            ConsoleUtils.WriteInfo($"翻译代理已启动 (127.0.0.1:{Port}, 主服务: {primaryName}{fallbackName}, 批量翻译: 启用 [队列{_batchSize}条/滑动窗口], 语言: {_defaultSourceLanguage}→{_defaultTargetLanguage})");
            return true;
        }
        catch (Exception ex)
        {
            IsRunning = false;
            DiagnosticLogger.Log($"[TranslationProxy Start] 异常: {ex.Message}");
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
                _ = ProcessClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (IOException) { break; }
            catch (SocketException) { break; }
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = TCP_RECEIVE_TIMEOUT_MS;
                client.SendTimeout = TCP_SEND_TIMEOUT_MS;

                using var stream = client.GetStream();
                var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                DiagnosticLogger.Log($"[Proxy TCP] 新连接来自: {remoteEp}");

                while (!ct.IsCancellationRequested && client.Connected)
                {
                    var keepAlive = false;

                    var requestLine = await ReadLineFromStreamAsync(stream, ct);
                    if (string.IsNullOrEmpty(requestLine)) break;

                    DiagnosticLogger.Log($"[Proxy HTTP] 请求行: {requestLine}");

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2) break;
                    var httpMethod = parts[0].ToUpperInvariant();
                    var url = parts[1];

                    if (httpMethod is not ("GET" or "POST" or "HEAD"))
                    {
                        DiagnosticLogger.Log($"[Proxy HTTP] 不支持的方法: {httpMethod}");
                        await WriteHttpResponse(stream, 405, "Method Not Allowed", "", false);
                        break;
                    }

                    int contentLength = 0;
                    string contentType = "";
                    string connectionHeader = "";
                    bool invalidContentLength = false;
                    while (true)
                    {
                        var line = await ReadLineFromStreamAsync(stream, ct);
                        if (string.IsNullOrEmpty(line)) break;
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            var valueStr = line.Substring("Content-Length:".Length).Trim();
                            if (!int.TryParse(valueStr, out contentLength))
                            {
                                // 必须继续读完所有 header 行再报错，否则剩余 header 留在流中
                                // 污染 keep-alive 连接的下一次请求解析。
                                DiagnosticLogger.Log($"[Proxy HTTP] 无效的 Content-Length: {valueStr}");
                                invalidContentLength = true;
                            }
                        }
                        if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                            contentType = line.Substring("Content-Type:".Length).Trim();
                        if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase))
                            connectionHeader = line.Substring("Connection:".Length).Trim();
                    }
                    if (invalidContentLength)
                    {
                        await WriteHttpResponse(stream, 400, "Bad Request", "", false);
                        break;
                    }

                    DiagnosticLogger.Log($"[Proxy HTTP] 方法: {httpMethod}, URL: {url}, ContentLength: {contentLength}, ContentType: {contentType}, Connection: {connectionHeader}");

                    keepAlive = connectionHeader.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);

                    if (httpMethod == "GET" && (url == "/health" || url == "/healthz"))
                    {
                        await WriteHttpResponse(stream, 200, "OK", "OK", keepAlive);
                        DiagnosticLogger.Log($"[Proxy HTTP] 响应: 200 health check");
                    }
                    else if (httpMethod == "POST")
                    {
                        await HandlePostRequestOptimized(stream, url, contentLength, contentType, keepAlive, ct);
                    }
                    else if (httpMethod == "GET")
                    {
                        await HandleGetRequest(stream, url, keepAlive, ct);
                    }
                    else
                    {
                        DiagnosticLogger.Log($"[Proxy HTTP] 不支持的方法: {httpMethod}");
                        await WriteHttpResponse(stream, 405, "Method Not Allowed", "", keepAlive);
                    }

                    if (!keepAlive) break;
                    await stream.FlushAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ex)
        {
            DiagnosticLogger.Log($"[Proxy TCP] IO异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"[Proxy TCP] 处理异常: {ex.Message}");
            ConsoleUtils.WriteDebug($"代理处理异常: {ex.Message}");
        }
    }

    private const int MAX_HEADER_LINE_LENGTH = 8192;

    private static async Task<string> ReadLineFromStreamAsync(Stream stream, CancellationToken ct)
    {
        // 固定大小缓冲区：避免无限内存增长，同时减少 GC 压力。
        // 逐字节读取仍保留（HTTP header 行通常很短，瓶颈不在这里）。
        var buffer = new byte[MAX_HEADER_LINE_LENGTH];
        int pos = 0;
        while (pos < MAX_HEADER_LINE_LENGTH)
        {
            var read = await stream.ReadAsync(buffer, pos, 1, ct);
            if (read == 0) return "";
            if (buffer[pos] == '\n')
            {
                var len = pos > 0 && buffer[pos - 1] == '\r' ? pos - 1 : pos;
                return Encoding.UTF8.GetString(buffer, 0, len);
            }
            pos++;
        }
        // 行超过最大长度：返回空字符串让上层断开连接，防止 OOM。
        DiagnosticLogger.Log($"[Proxy HTTP] Header 行超过 {MAX_HEADER_LINE_LENGTH} 字节，断开连接");
        return "";
    }

    private async Task HandlePostRequestOptimized(NetworkStream stream, string url, int contentLength, string contentType, bool keepAlive, CancellationToken ct)
    {
        DiagnosticLogger.Log($"[Proxy POST] URL: {url}, ContentLength: {contentLength}, ContentType: {contentType}");

        if (!url.StartsWith("/translate"))
        {
            DiagnosticLogger.Log($"[Proxy POST] URL不匹配 /translate: {url}");
            await WriteHttpResponse(stream, 404, "Not Found", "", keepAlive);
            return;
        }

        if (contentLength <= 0 || contentLength > MAX_CONTENT_LENGTH)
        {
            DiagnosticLogger.Log($"[Proxy POST] ContentLength无效: {contentLength}");
            await WriteHttpResponse(stream, 400, "Bad Request", "", keepAlive);
            return;
        }

        var bodyBytes = new byte[contentLength];
        var bytesRead = 0;
        while (bytesRead < contentLength)
        {
            var n = await stream.ReadAsync(bodyBytes, bytesRead, contentLength - bytesRead, ct);
            if (n == 0) break;
            bytesRead += n;
        }

        if (bytesRead < contentLength)
        {
            // 连接中途断开，残缺 body 不可信，直接拒绝，避免向 LLM 发送乱码。
            DiagnosticLogger.Log($"[Proxy POST] Body 不完整: 期望 {contentLength} 字节，实际读取 {bytesRead} 字节");
            await WriteHttpResponse(stream, 400, "Bad Request", "", keepAlive);
            return;
        }

        var body = Encoding.UTF8.GetString(bodyBytes, 0, bytesRead);

        DiagnosticLogger.Log($"[Proxy POST Body] 前200字符: {(body.Length > LOG_TRUNCATE_LENGTH ? body[..LOG_TRUNCATE_LENGTH] + "..." : body)}");

        var sourceText = ExtractSourceText(body, contentType);

        DiagnosticLogger.Log($"[Proxy POST] 提取到文本长度: {sourceText?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            Interlocked.Increment(ref _failCount);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 400, "Bad Request", "", keepAlive);
            return;
        }

        var (filteredText, placeholderMap) = GameTextFilter.Filter(sourceText);

        var cacheKey = ComputeCacheKey(filteredText);
        if (_resultCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Interlocked.Increment(ref _cacheHits);
            Interlocked.Increment(ref _successCount);
            var restored = GameTextFilter.Restore(cachedResult.TranslatedText, placeholderMap);
            OnTranslation?.Invoke(sourceText, restored, null);
            HookLogger.Log(sourceText, restored, null);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 200, "OK", restored, keepAlive);
            return;
        }

        if (GameTextFilter.ContainsOnlyControlChars(filteredText))
        {
            Interlocked.Increment(ref _cacheHits);
            Interlocked.Increment(ref _successCount);
            AddToCache(cacheKey, sourceText);
            OnTranslation?.Invoke(sourceText, sourceText, null);
            HookLogger.Log(sourceText, sourceText, null);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 200, "OK", sourceText, keepAlive);
            return;
        }

        Interlocked.Increment(ref _cacheMisses);
        Interlocked.Increment(ref _totalRequests);
        var preview = sourceText.Length > 50 ? sourceText[..50] + "..." : sourceText;
        ConsoleUtils.WriteDebug($"翻译请求 [{TotalRequests}]: {preview}");

        var sw = Stopwatch.StartNew();

        try
        {
            string? translatedText;

            if (_batchModeEnabled)
            {
                translatedText = await TranslateViaBatchAsync(filteredText, cacheKey, placeholderMap, _defaultSourceLanguage, _defaultTargetLanguage, ct);
            }
            else
            {
                translatedText = await TranslateDeduplicatedAsync(filteredText, cacheKey, _defaultSourceLanguage, _defaultTargetLanguage, ct);
            }

            sw.Stop();

            if (translatedText != null)
            {
                Interlocked.Add(ref _totalLatencyMs, sw.ElapsedMilliseconds);
                Interlocked.Increment(ref _latencyCount);

                var restored = GameTextFilter.Restore(translatedText, placeholderMap);
                Interlocked.Increment(ref _successCount);
                AddToCache(cacheKey, translatedText);
                OnTranslation?.Invoke(sourceText, restored, null);
                HookLogger.Log(sourceText, restored, null);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                ConsoleUtils.WriteDebug($"翻译耗时: {sw.ElapsedMilliseconds}ms");
                await WriteHttpResponse(stream, 200, "OK", restored, keepAlive);
            }
            else
            {
                Interlocked.Increment(ref _failCount);
                OnTranslation?.Invoke(sourceText, "", LastError);
                HookLogger.Log(sourceText, "", LastError);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 500, "Error", "", keepAlive);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failCount);
            LastError = ex.Message;
            ConsoleUtils.WriteError($"翻译异常: {ex.Message}");
            OnTranslation?.Invoke(sourceText, "", ex.Message);
            HookLogger.Log(sourceText, "", ex.Message);
            OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
            await WriteHttpResponse(stream, 500, "Error", "", keepAlive);
        }
    }

    private async Task<string?> TranslateViaBatchAsync(string filteredText, string cacheKey, Dictionary<string, string> placeholderMap, string sourceLang, string targetLang, CancellationToken ct)
    {
        var batchId = Interlocked.Increment(ref _batchIdCounter);
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var batchItem = new BatchItem
        {
            Id = batchId,
            FilteredText = filteredText,
            PlaceholderMap = placeholderMap,
            CacheKey = cacheKey,
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Tcs = tcs
        };

        _batchQueue.Enqueue(batchItem);
        var count = _batchQueue.Count;

        if (count >= _batchSize)
        {
            CancelBatchTimer();
            _batchSignal.Release();
        }
        else if (count == 1)
        {
            StartBatchTimer();
        }

        using var ctr = ct.Register(() => tcs.TrySetCanceled(ct));
        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private void StartBatchTimer()
    {
        lock (_batchTimerLock)
        {
            CancelBatchTimerLocked();
            _batchTimerCts = new CancellationTokenSource();
            var capturedCts = _batchTimerCts;
            var timeoutMs = _consecutiveSingleItemBatches >= ADAPTIVE_TIMEOUT_THRESHOLD
                ? REDUCED_BATCH_TIMEOUT_MS
                : _appConfig.BatchTimeoutMs;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(timeoutMs, capturedCts.Token);
                    if (!_batchQueue.IsEmpty)
                        _batchSignal.Release();
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
            });
        }
    }

    private void CancelBatchTimer()
    {
        lock (_batchTimerLock)
        {
            CancelBatchTimerLocked();
        }
    }

    private void CancelBatchTimerLocked()
    {
        var oldCts = _batchTimerCts;
        _batchTimerCts = null;
        if (oldCts != null)
        {
            try { oldCts.Cancel(); } catch { }
            try { oldCts.Dispose(); } catch { }
        }
    }

    private async Task BatchFlushLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _batchSignal.WaitAsync(ct);

                if (!_batchQueue.IsEmpty)
                {
                    CancelBatchTimer();
                    await FlushBatchAsync(ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                ConsoleUtils.WriteError($"批量发送异常: {ex.Message}");
            }
        }
    }

    public void FlushRemainingBatch()
    {
        CancelBatchTimer();
        if (!_batchQueue.IsEmpty)
        {
            _batchSignal.Release();
        }
    }

    private async Task FlushBatchAsync(CancellationToken ct)
    {
        var items = new List<BatchItem>();
        while (_batchQueue.TryDequeue(out var item))
        {
            items.Add(item);
            if (items.Count >= _batchSize * BATCH_SIZE_OVERFLOW_MULTIPLIER)
                break;
        }

        if (items.Count == 0) return;

        if (items.Count <= 1)
            _consecutiveSingleItemBatches++;
        else
            _consecutiveSingleItemBatches = 0;

        DiagnosticLogger.Log($"[FlushBatch] 实际批量大小: {items.Count}, 队列剩余: {_batchQueue.Count}");

        await _concurrencyLimiter.WaitAsync(ct);
        bool needDegraded = false;
        try
        {
            ConsoleUtils.WriteDebug($"批量发送 {items.Count} 条翻译请求");

            var provider = _primaryProviders[0];

            var sourceLang = items[0].SourceLanguage;
            var targetLang = items[0].TargetLanguage;

            var batchRequest = new ProviderBatchTranslationRequest
            {
                Items = items.Select(i => new ProviderBatchItem { Id = i.Id, Text = i.FilteredText }).ToList(),
                SourceLanguage = sourceLang,
                TargetLanguage = targetLang,
                MaxTokens = DEFAULT_MAX_TOKENS
            };

            bool acquired = false;
            for (int rpmAttempt = 0; rpmAttempt < RPM_RETRY_ATTEMPTS; rpmAttempt++)
            {
                if (provider.RpmLimiter.TryAcquire())
                {
                    acquired = true;
                    break;
                }
                ConsoleUtils.WriteWarning($"主服务 {provider.ProviderName} RPM 已满，等待重试 ({rpmAttempt + 1}/{RPM_RETRY_ATTEMPTS})");
                await Task.Delay(RPM_RETRY_DELAY_MS, ct);
            }

            if (!acquired)
            {
                if (_fallbackProvider != null)
                {
                    ConsoleUtils.WriteInfo($"主服务 RPM 超限，批量请求转至备用服务");
                    for (int i = 0; i < RPM_RETRY_ATTEMPTS; i++)
                    {
                        if (_fallbackProvider.RpmLimiter.TryAcquire()) break;
                        await Task.Delay(RPM_RETRY_DELAY_MS, ct);
                    }

                    var fallbackRpmResult = await _fallbackProvider.TranslateBatchAsync(batchRequest, ct);
                    if (fallbackRpmResult.Success)
                    {
                        ProcessBatchSuccess(fallbackRpmResult, items, ct);
                        return;
                    }
                    // 备用批量失败（无论可重试/需转移/不可恢复），统一降级为单条翻译
                    needDegraded = true;
                }
                else
                {
                    ConsoleUtils.WriteError($"批量翻译失败: RPM 限制超时且无可用备用服务");
                    needDegraded = true;
                }
            }
            else
            {
                var result = await provider.TranslateBatchAsync(batchRequest, ct);

                DiagnosticLogger.Log($"[FlushBatch] 批量结果: Success={result.Success}, TranslatedTexts数量={result.TranslatedTexts.Count}, Error={result.ErrorMessage}");

                if (result.Success)
                {
                    ProcessBatchSuccess(result, items, ct);
                }
                else
                {
                    ConsoleUtils.WriteError($"批量翻译失败 ({provider.ProviderName}): {result.ErrorMessage}");

                    bool recovered = false;

                    if (result.ShouldRetry)
                    {
                        recovered = await RetryBatchTranslation(provider, batchRequest, items, ct);
                    }

                    if (!recovered && result.ShouldFailover && _fallbackProvider != null)
                    {
                        recovered = await FailoverBatchTranslation(batchRequest, items, ct);
                    }

                    if (!recovered)
                    {
                        needDegraded = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"批量翻译异常: {ex.Message}");
            needDegraded = true;
        }
        finally
        {
            _concurrencyLimiter.Release();
        }

        if (needDegraded)
        {
            await DegradedSingleTranslate(items, ct);
        }
    }

    private async Task<bool> RetryBatchTranslation(IModelProvider provider, ProviderBatchTranslationRequest batchRequest, List<BatchItem> items, CancellationToken ct)
    {
        for (int retry = 0; retry < MAX_RETRY_ATTEMPTS; retry++)
        {
            var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retry + 1) * 500);
            ConsoleUtils.WriteDebug($"批量重试 {provider.ProviderName} (第{retry + 1}次), 等待 {delay.TotalMilliseconds}ms...");
            await Task.Delay(delay, ct);

            for (int rpmAttempt = 0; rpmAttempt < RPM_RETRY_ATTEMPTS; rpmAttempt++)
            {
                if (provider.RpmLimiter.TryAcquire()) break;
                if (rpmAttempt < RPM_RETRY_ATTEMPTS - 1)
                    await Task.Delay(RPM_RETRY_DELAY_MS, ct);
            }

            var retryResult = await provider.TranslateBatchAsync(batchRequest, ct);
            if (retryResult.Success)
            {
                ProcessBatchSuccess(retryResult, items, ct);
                return true;
            }
            if (!retryResult.ShouldRetry)
                break;
        }
        return false;
    }

    private async Task<bool> FailoverBatchTranslation(ProviderBatchTranslationRequest batchRequest, List<BatchItem> items, CancellationToken ct)
    {
        ConsoleUtils.WriteInfo($"批量翻译切换到备用服务: {_fallbackProvider!.ProviderName}");
        for (int i = 0; i < RPM_RETRY_ATTEMPTS; i++)
        {
            if (_fallbackProvider.RpmLimiter.TryAcquire()) break;
            await Task.Delay(RPM_RETRY_DELAY_MS, ct);
        }

        var fallbackResult = await _fallbackProvider.TranslateBatchAsync(batchRequest, ct);
        if (fallbackResult.Success)
        {
            ProcessBatchSuccess(fallbackResult, items, ct);
            return true;
        }

        if (fallbackResult.ShouldRetry)
        {
            return await RetryBatchTranslation(_fallbackProvider, batchRequest, items, ct);
        }
        return false;
    }

    private void ProcessBatchSuccess(ProviderBatchTranslationResult result, List<BatchItem> items, CancellationToken ct)
    {
        var fallbackCount = result.FallbackItemIds?.Count ?? 0;

        if (fallbackCount > 0)
        {
            ConsoleUtils.WriteWarning($"批量翻译部分失败: {result.TranslatedTexts.Count - fallbackCount}/{result.TotalRequested} 条成功翻译, {fallbackCount} 条用原文兜底");
        }

        foreach (var item in items)
        {
            if (result.FallbackItemIds?.Contains(item.Id) == true)
            {
                DiagnosticLogger.Log($"[FlushBatch] ID={item.Id} 兜底返回原文");
                item.Tcs.TrySetResult(item.FilteredText);
            }
            else if (result.TranslatedTexts.TryGetValue(item.Id, out var translated))
            {
                DiagnosticLogger.Log($"[FlushBatch] ID={item.Id} 翻译成功: 长度={translated.Length}, 首20字符={(translated.Length > 20 ? translated[..20] + "..." : translated)}");
                item.Tcs.TrySetResult(translated);
            }
            else
            {
                DiagnosticLogger.Log($"[FlushBatch] ID={item.Id} 未找到翻译结果，兜底返回原文");
                item.Tcs.TrySetResult(item.FilteredText);
            }
        }

        OnBatchFlushed?.Invoke(items.Count - fallbackCount);
    }

    private async Task DegradedSingleTranslate(List<BatchItem> items, CancellationToken ct)
    {
        ConsoleUtils.WriteWarning($"批量翻译不可用，退化为逐条翻译 ({items.Count} 条)");
        var tasks = items.Select(item => TranslateSingleAndCompleteAsync(item, ct)).ToArray();
        try { await Task.WhenAll(tasks); }
        catch { }
    }

    private async Task TranslateSingleAndCompleteAsync(BatchItem item, CancellationToken ct)
    {
        try
        {
            var result = await TranslateDeduplicatedAsync(item.FilteredText, item.CacheKey, item.SourceLanguage, item.TargetLanguage, ct);
            item.Tcs.TrySetResult(result ?? item.FilteredText);
        }
        catch (Exception ex)
        {
            item.Tcs.TrySetResult(item.FilteredText);
            ConsoleUtils.WriteDebug($"单条降级翻译失败，返回原文: {ex.Message}");
        }
    }

    private async Task<string?> TranslateDeduplicatedAsync(string sourceText, string cacheKey, string sourceLang, string targetLang, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string?>();
        var existingTask = _inFlightRequests.GetOrAdd(cacheKey, tcs.Task);

        if (existingTask != tcs.Task)
        {
            try
            {
                return await existingTask;
            }
            catch (AggregateException ae)
            {
                return ae.InnerException is null ? null : throw ae.InnerException;
            }
        }

        await _concurrencyLimiter.WaitAsync(ct);
        try
        {
            if (_resultCache.TryGetValue(cacheKey, out var cached))
            {
                tcs.SetResult(cached.TranslatedText);
                return cached.TranslatedText;
            }

            var result = await TranslateWithFailoverAsync(sourceText, sourceLang, targetLang, ct);
            if (result != null)
            {
                AddToCache(cacheKey, result);
            }
            tcs.SetResult(result);
            return result;
        }
        catch (Exception)
        {
            tcs.SetResult(null);
            return null;
        }
        finally
        {
            _concurrencyLimiter.Release();
            _inFlightRequests.TryRemove(cacheKey, out _);
        }
    }

    private async Task<string?> TranslateWithFailoverAsync(string sourceText, string sourceLang, string targetLang, CancellationToken ct)
    {
        var request = new ProviderTranslationRequest
        {
            SourceText = sourceText,
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Temperature = 0,
            MaxTokens = DEFAULT_MAX_TOKENS
        };

        foreach (var provider in _primaryProviders)
        {
            if (!provider.RpmLimiter.TryAcquire())
            {
                var currentRpm = provider.RpmLimiter.GetCurrentRpm();
                ConsoleUtils.WriteDebug($"主服务 RPM 接近上限 ({currentRpm}/{provider.RpmLimiter.MaxRpm})，等待 200ms...");
                await Task.Delay(200, ct);
                if (!provider.RpmLimiter.TryAcquire())
                {
                    ConsoleUtils.WriteWarning($"主服务 {provider.ProviderName} RPM 已满 ({currentRpm}/{provider.RpmLimiter.MaxRpm})，切换到备用服务");
                    break;
                }
            }

            var result = await TranslateWithRetryAsync(provider, request, ct);
            if (result.Success)
                return result.TranslatedText;

            if (result.ShouldFailover)
            {
                ConsoleUtils.WriteWarning($"主服务 {provider.ProviderName} 不可用: {result.ErrorMessage}，尝试切换...");
                break;
            }

            if (!result.ShouldRetry)
                break;
        }

        if (_fallbackProvider != null)
        {
            ConsoleUtils.WriteInfo($"切换到备用服务: {_fallbackProvider.ProviderName}");

            while (!_fallbackProvider.RpmLimiter.TryAcquire())
            {
                ConsoleUtils.WriteDebug($"备用服务 RPM 超限，等待 1 秒后重试...");
                await Task.Delay(1000, ct);
            }

            var fallbackResult = await TranslateWithRetryAsync(_fallbackProvider, request, ct);
            if (fallbackResult.Success)
            {
                ConsoleUtils.WriteSuccess($"备用服务 {_fallbackProvider.ProviderName} 翻译成功");
                return fallbackResult.TranslatedText;
            }

            LastError = $"所有服务不可用 (主+备): {fallbackResult.ErrorMessage}";
            ConsoleUtils.WriteError(LastError);
            return null;
        }

        return null;
    }

    private static async Task<ProviderTranslationResult> TranslateWithRetryAsync(IModelProvider provider, ProviderTranslationRequest request, CancellationToken ct)
    {
        ProviderTranslationResult? lastResult = null;

        for (int attempt = 0; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 500);
                ConsoleUtils.WriteDebug($"重试 {provider.ProviderName} (第{attempt}次), 等待 {delay.TotalMilliseconds}ms...");
                await Task.Delay(delay, ct);
            }

            var result = await provider.TranslateAsync(request, ct);
            lastResult = result;

            if (result.Success)
                return result;

            if (!result.ShouldRetry)
                return result;

            ConsoleUtils.WriteDebug($"{provider.ProviderName} 请求失败: {result.ErrorMessage}");
        }

        return lastResult ?? new ProviderTranslationResult
        {
            Success = false,
            ErrorMessage = "未知错误",
            ProviderName = provider.ProviderName,
            ShouldFailover = true
        };
    }

    private async Task CacheCleanerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                CleanExpiredCache();
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private void CleanExpiredCache()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
        var expiredKeys = _resultCache
            .Where(x => x.Value.Timestamp < cutoff)
            .Select(x => x.Key)
            .ToHashSet();

        if (expiredKeys.Count == 0) return;

        foreach (var key in expiredKeys)
            _resultCache.TryRemove(key, out _);

        // 不再 drain+重建 LRU 队列（会丢失并发新增的 key）。
        // 过期 key 留在队列中，EvictOldestCacheEntry 的 TryRemove 会自动跳过已移除的 key。
        DiagnosticLogger.Log($"[CacheClean] 清除过期条目: {expiredKeys.Count}，剩余: {_resultCache.Count}");
    }

    private void AddToCache(string key, string translatedText)
    {
        if (_resultCache.Count >= MAX_CACHE_SIZE)
            EvictOldestCacheEntry();

        var entry = new TranslationResult { TranslatedText = translatedText, Timestamp = DateTime.UtcNow };
        // TryAdd 是原子操作：只有第一个到达的线程成功，避免竞态下 key 在 LRU 队列中双重入队。
        if (_resultCache.TryAdd(key, entry))
            _cacheKeyOrder.Enqueue(key);
    }

    private void EvictOldestCacheEntry()
    {
        // 从 LRU 队列头部取 key，跳过已被 TTL 清理器移除的 key，直到成功驱逐一条。
        var attempts = 0;
        while (_cacheKeyOrder.TryDequeue(out var oldKey) && attempts < MAX_CACHE_SIZE)
        {
            attempts++;
            if (_resultCache.TryRemove(oldKey, out _))
                return;
        }
    }

    private static string ComputeCacheKey(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash);
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

                if (root.TryGetProperty("q", out var qProp))
                    return qProp.GetString() ?? "";
            }
            catch { }
            return "";
        }

        // 只需含 '=' 即尝试 form 解析（含 & 的多参数或单参数 text=value 均可命中）。
        // 原先要求同时含 & 和 = 会漏掉 XUnity 发送的单参数请求，导致 "text=hello" 整体
        // 被当作源文本传给 LLM。
        if (isFormUrlEncoded || body.Contains('='))
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

    private async Task HandleGetRequest(NetworkStream stream, string url, bool keepAlive, CancellationToken ct)
    {
        DiagnosticLogger.Log($"[Proxy GET] URL: {url}");

        try
        {
            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart >= url.Length - 1)
            {
                DiagnosticLogger.Log($"[Proxy GET] 无查询参数");
                await WriteHttpResponse(stream, 400, "Bad Request", "", keepAlive);
                return;
            }

            var query = url[(queryStart + 1)..];
            var text = GetQueryParam(query, "text") ?? GetQueryParam(query, "q") ?? "";

            DiagnosticLogger.Log($"[Proxy GET] 提取文本长度: {text.Length}");

            if (string.IsNullOrWhiteSpace(text))
            {
                DiagnosticLogger.Log($"[Proxy GET] 文本为空");
                await WriteHttpResponse(stream, 400, "Bad Request", "", keepAlive);
                return;
            }

            var sourceLang = GetQueryParam(query, "from") ?? GetQueryParam(query, "source") ?? _defaultSourceLanguage;
            var targetLang = GetQueryParam(query, "to") ?? GetQueryParam(query, "target") ?? _defaultTargetLanguage;

            var (filteredText, placeholderMap) = GameTextFilter.Filter(text);

            var cacheKey = ComputeCacheKey(filteredText);
            if (_resultCache.TryGetValue(cacheKey, out var cachedResult))
            {
                Interlocked.Increment(ref _cacheHits);
                Interlocked.Increment(ref _successCount);
                var restored = GameTextFilter.Restore(cachedResult.TranslatedText, placeholderMap);
                OnTranslation?.Invoke(text, restored, null);
                HookLogger.Log(text, restored, null);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 200, "OK", restored, keepAlive);
                return;
            }

            if (GameTextFilter.ContainsOnlyControlChars(filteredText))
            {
                Interlocked.Increment(ref _cacheHits);
                Interlocked.Increment(ref _successCount);
                AddToCache(cacheKey, text);
                OnTranslation?.Invoke(text, text, null);
                HookLogger.Log(text, text, null);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 200, "OK", text, keepAlive);
                return;
            }

            Interlocked.Increment(ref _cacheMisses);
            Interlocked.Increment(ref _totalRequests);

            string? result;
            if (_batchModeEnabled)
            {
                result = await TranslateViaBatchAsync(filteredText, cacheKey, placeholderMap, sourceLang, targetLang, ct);
            }
            else
            {
                result = await TranslateDeduplicatedAsync(filteredText, cacheKey, sourceLang, targetLang, ct);
            }

            if (result != null)
            {
                AddToCache(cacheKey, result);
                var restored = GameTextFilter.Restore(result, placeholderMap);
                Interlocked.Increment(ref _successCount);
                OnTranslation?.Invoke(text, restored, null);
                HookLogger.Log(text, restored, null);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 200, "OK", restored, keepAlive);
            }
            else
            {
                Interlocked.Increment(ref _failCount);
                OnTranslation?.Invoke(text, "", LastError);
                HookLogger.Log(text, "", LastError);
                OnStatisticsChanged?.Invoke(TotalRequests, SuccessCount, FailCount);
                await WriteHttpResponse(stream, 500, "Error", "", keepAlive);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"[Proxy GET] 异常: {ex.GetType().Name}: {ex.Message}");
            Interlocked.Increment(ref _failCount);
            try
            {
                await WriteHttpResponse(stream, 500, "Error", "", keepAlive);
            }
            catch { }
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

    private static async Task WriteHttpResponse(NetworkStream stream, int statusCode, string statusText, string body, bool keepAlive = false)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var connection = keepAlive ? "keep-alive" : "close";
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                     $"Content-Type: text/plain; charset=utf-8\r\n" +
                     $"Content-Length: {bodyBytes.Length}\r\n" +
                     $"Connection: {connection}\r\n" +
                     (keepAlive ? $"Keep-Alive: timeout={KEEP_ALIVE_TIMEOUT_SEC}, max={KEEP_ALIVE_MAX_REQUESTS}\r\n" : "") +
                     $"Access-Control-Allow-Origin: http://localhost\r\n" +
                     $"\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(bodyBytes);
    }

    public void Dispose()
    {
        IsRunning = false;
        _cts.Cancel();

        CancelBatchTimer();
        try { _batchFlushCts?.Cancel(); } catch { }
        try { _batchSignal.Dispose(); } catch { }
        try { _batchFlushCts?.Dispose(); } catch { }
        try { _batchTimerCts?.Dispose(); } catch { }

        try { _rpmRefreshTimer.Stop(); _rpmRefreshTimer.Dispose(); } catch { }
        try { _listener?.Stop(); } catch { }
        _concurrencyLimiter.Dispose();
        _cts.Dispose();

        foreach (var provider in _primaryProviders)
        {
            try { provider.Dispose(); } catch { }
        }
        try { _fallbackProvider?.Dispose(); } catch { }

        while (_batchQueue.TryDequeue(out var item))
        {
            item.Tcs.TrySetResult(null);
        }
    }

    private class BatchItem
    {
        public long Id { get; init; }
        public string FilteredText { get; init; } = "";
        public Dictionary<string, string> PlaceholderMap { get; init; } = new();
        public string CacheKey { get; init; } = "";
        public string SourceLanguage { get; init; } = "ja";
        public string TargetLanguage { get; init; } = "zh";
        public TaskCompletionSource<string?> Tcs { get; init; } = null!;
    }

    private class TranslationResult
    {
        public string TranslatedText { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
