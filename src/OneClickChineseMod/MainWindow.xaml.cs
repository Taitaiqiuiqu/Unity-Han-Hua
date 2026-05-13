using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using OneClickChineseMod.Core;
using OneClickChineseMod.Core.Providers;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);

    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private AppConfig _appConfig;
    private PluginDeployer? _pluginDeployer;
    private TranslationProxyOptimized? _translationProxy;
    private GameInfo? _currentGame;
    private string? _selectedExePath;
    private bool _isWorking;
    private bool _isInitialized;
    private bool _isLoadingConfig;
    private string _currentPage = "home";

    private static readonly Dictionary<ModelProvider, string> ProviderComboBoxNames = new()
    {
        [ModelProvider.SiliconFlow] = "硅基流动",
        [ModelProvider.Qwen] = "阿里通义千问",
        [ModelProvider.Hunyuan] = "腾讯混元",
        [ModelProvider.Doubao] = "字节豆包",
        [ModelProvider.Ernie] = "百度文心一言",
        [ModelProvider.DeepSeek] = "DeepSeek"
    };

    public MainWindow()
    {
        InitializeComponent();
        _appConfig = AppConfig.Load();
        DiagnosticLogger.Log($"[MainWindow] 启动, 配置版本: {_appConfig.ConfigVersion}, Providers数量: {_appConfig.Providers.Count}, SourceLang: {_appConfig.SourceLanguage}, TargetLang: {_appConfig.TargetLanguage}");
        ConsoleUtils.OnLog += OnLogMessage;
        LoadConfigToUI();
        _isInitialized = true;
        SliderConcurrency.ValueChanged += SliderConcurrency_ValueChanged;
        ResetToIdle();
        NavigateTo("home");
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch { }
    }

    private void NavigateTo(string page)
    {
        _currentPage = page;

        HomePage.Visibility = Visibility.Collapsed;
        TranslatePage.Visibility = Visibility.Collapsed;
        ConfigPage.Visibility = Visibility.Collapsed;
        AboutPage.Visibility = Visibility.Collapsed;

        NavHome.Foreground = FindResource("TertiaryTextBrush") as Brush;
        NavTranslate.Foreground = FindResource("TertiaryTextBrush") as Brush;
        NavConfig.Foreground = FindResource("TertiaryTextBrush") as Brush;
        NavAbout.Foreground = FindResource("TertiaryTextBrush") as Brush;

        switch (page)
        {
            case "home":
                HomePage.Visibility = Visibility.Visible;
                NavHome.Foreground = FindResource("PrimaryTextBrush") as Brush;
                break;
            case "translate":
                TranslatePage.Visibility = Visibility.Visible;
                NavTranslate.Foreground = FindResource("PrimaryTextBrush") as Brush;
                break;
            case "config":
                ConfigPage.Visibility = Visibility.Visible;
                NavConfig.Foreground = FindResource("PrimaryTextBrush") as Brush;
                break;
            case "about":
                AboutPage.Visibility = Visibility.Visible;
                NavAbout.Foreground = FindResource("PrimaryTextBrush") as Brush;
                break;
        }

        UpdateTabIndicator(page);
    }

    private void UpdateTabIndicator(string page)
    {
        double offset = page switch
        {
            "home" => 0,
            "translate" => 56,
            "config" => 112,
            "about" => 168,
            _ => 0
        };

        Canvas.SetLeft(TabIndicator, offset + 24);
    }

    private void Logo_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateTo("home");
    }

    private void NavHome_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("home");
    }

    private void NavTranslate_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("translate");
    }

    private void NavConfig_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("config");
    }

    private void NavAbout_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("about");
    }

    private void FeatureCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void LoadConfigToUI()
    {
        _isLoadingConfig = true;
        try
        {
            var primary = _appConfig.GetPrimaryProvider();
            if (primary != null)
            {
                CmbProvider.SelectedIndex = ProviderToComboBoxIndex(primary.Provider);
                TxtApiKey.Password = primary.ApiKey;
                TxtSecretKey.Password = primary.SecretKey ?? "";
                UpdateModelComboBox(CmbModel, primary.Provider, primary.ModelName);
                UpdateProviderUI(primary.Provider);
            }
            else
            {
                CmbProvider.SelectedIndex = 0;
                UpdateModelComboBox(CmbModel, ModelProvider.SiliconFlow, "deepseek-ai/DeepSeek-V4-Flash");
                UpdateProviderUI(ModelProvider.SiliconFlow);
            }

            var fallback = _appConfig.GetFallbackProvider();
            if (fallback != null)
            {
                CmbFallbackProvider.SelectedIndex = ProviderToFallbackComboBoxIndex(fallback.Provider);
                TxtFallbackApiKey.Password = fallback.ApiKey;
                TxtFallbackSecretKey.Password = fallback.SecretKey ?? "";
                UpdateModelComboBox(CmbFallbackModel, fallback.Provider, fallback.ModelName);
                UpdateFallbackProviderUI(fallback.Provider);
            }
            else
            {
                CmbFallbackProvider.SelectedIndex = 0;
                UpdateFallbackProviderUI(null);
            }

            SliderConcurrency.Value = _appConfig.MaxConcurrency;
            TxtConcurrencyValue.Text = _appConfig.MaxConcurrency.ToString();

            SliderRpm.Value = _appConfig.MaxRpm;
            TxtRpmValue.Text = _appConfig.MaxRpm.ToString();

            ChkBatchEnabled.IsChecked = _appConfig.BatchModeEnabled;
            SliderBatchSize.Value = _appConfig.BatchSize;
            TxtBatchSizeValue.Text = _appConfig.BatchSize.ToString();

            CmbSourceLang.SelectedIndex = AppConfig.SourceLanguageToIndex(_appConfig.SourceLanguage);
            CmbTargetLang.SelectedIndex = AppConfig.TargetLanguageToIndex(_appConfig.TargetLanguage);
        }
        finally
        {
            _isLoadingConfig = false;
        }
    }

    private static int ProviderToComboBoxIndex(ModelProvider provider)
    {
        return provider switch
        {
            ModelProvider.SiliconFlow => 0,
            ModelProvider.Qwen => 1,
            ModelProvider.Hunyuan => 2,
            ModelProvider.Doubao => 3,
            ModelProvider.Ernie => 4,
            ModelProvider.DeepSeek => 5,
            _ => 0
        };
    }

    private static ModelProvider ComboBoxIndexToProvider(int index)
    {
        return index switch
        {
            0 => ModelProvider.SiliconFlow,
            1 => ModelProvider.Qwen,
            2 => ModelProvider.Hunyuan,
            3 => ModelProvider.Doubao,
            4 => ModelProvider.Ernie,
            5 => ModelProvider.DeepSeek,
            _ => ModelProvider.SiliconFlow
        };
    }

    private static int ProviderToFallbackComboBoxIndex(ModelProvider provider)
    {
        return provider switch
        {
            ModelProvider.SiliconFlow => 1,
            ModelProvider.Qwen => 2,
            ModelProvider.Hunyuan => 3,
            ModelProvider.Doubao => 4,
            ModelProvider.Ernie => 5,
            ModelProvider.DeepSeek => 6,
            _ => 0
        };
    }

    private static ModelProvider? FallbackComboBoxIndexToProvider(int index)
    {
        return index switch
        {
            0 => null,
            1 => ModelProvider.SiliconFlow,
            2 => ModelProvider.Qwen,
            3 => ModelProvider.Hunyuan,
            4 => ModelProvider.Doubao,
            5 => ModelProvider.Ernie,
            6 => ModelProvider.DeepSeek,
            _ => null
        };
    }

    private void UpdateModelComboBox(ComboBox comboBox, ModelProvider provider, string? selectedModel)
    {
        comboBox.Items.Clear();
        var models = ProviderFactory.GetAvailableModels(provider);
        int selectIndex = -1;

        for (int i = 0; i < models.Count; i++)
        {
            var tempConfig = new ProviderConfig { Provider = provider, ModelName = models[i] };
            var displayName = $"{models[i]} - {tempConfig.GetModelDisplayName()}";
            comboBox.Items.Add(displayName);

            if (models[i] == selectedModel)
                selectIndex = i;
        }

        if (selectedModel != null && !models.Contains(selectedModel) && !string.IsNullOrWhiteSpace(selectedModel))
        {
            comboBox.Items.Add($"{selectedModel} - 自定义模型");
            selectIndex = comboBox.Items.Count - 1;
        }

        if (selectIndex >= 0 && selectIndex < comboBox.Items.Count)
            comboBox.SelectedIndex = selectIndex;
        else if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
        else if (selectedModel != null)
            comboBox.Text = selectedModel;
    }

    private static string GetSelectedModelName(ModelProvider provider, ComboBox comboBox)
    {
        var text = comboBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return ProviderFactory.GetAvailableModels(provider).FirstOrDefault() ?? "deepseek-ai/DeepSeek-V4-Flash";

        var dashIndex = text.IndexOf(" - ");
        return dashIndex > 0 ? text[..dashIndex].Trim() : text;
    }

    private void UpdateProviderUI(ModelProvider provider)
    {
        LblApiKey.Text = ProviderFactory.GetApiKeyLabel(provider);
        LblApiKeyHint.Text = $"在 {ProviderFactory.GetApiKeyHint(provider)} 获取 API Key";
        PanelSecretKey.Visibility = ProviderFactory.RequiresSecretKey(provider)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateFallbackProviderUI(ModelProvider? provider)
    {
        var hasFallback = provider != null;
        LblFallbackApiKey.Visibility = hasFallback ? Visibility.Visible : Visibility.Collapsed;
        TxtFallbackApiKey.Visibility = hasFallback ? Visibility.Visible : Visibility.Collapsed;
        LblFallbackApiKeyHint.Visibility = hasFallback ? Visibility.Visible : Visibility.Collapsed;
        PanelFallbackSecretKey.Visibility = (hasFallback && ProviderFactory.RequiresSecretKey(provider!.Value))
            ? Visibility.Visible : Visibility.Collapsed;
        LblFallbackModel.Visibility = hasFallback ? Visibility.Visible : Visibility.Collapsed;
        CmbFallbackModel.Visibility = hasFallback ? Visibility.Visible : Visibility.Collapsed;

        if (hasFallback)
        {
            LblFallbackApiKey.Text = $"备用 {ProviderFactory.GetApiKeyLabel(provider!.Value)}";
            LblFallbackApiKeyHint.Text = $"在 {ProviderFactory.GetApiKeyHint(provider!.Value)} 获取 API Key";
        }
    }

    private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isLoadingConfig || CmbProvider.SelectedIndex < 0) return;
        var provider = ComboBoxIndexToProvider(CmbProvider.SelectedIndex);
        UpdateProviderUI(provider);
        UpdateModelComboBox(CmbModel, provider, null);
    }

    private void CmbFallbackProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _isLoadingConfig || CmbFallbackProvider.SelectedIndex < 0) return;
        var provider = FallbackComboBoxIndexToProvider(CmbFallbackProvider.SelectedIndex);
        UpdateFallbackProviderUI(provider);
        if (provider != null)
        {
            UpdateModelComboBox(CmbFallbackModel, provider.Value, null);
        }
    }

    private void SliderConcurrency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)e.NewValue;
        TxtConcurrencyValue.Text = value.ToString();
    }

    private void SliderBatchSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        var value = (int)e.NewValue;
        TxtBatchSizeValue.Text = value.ToString();
    }

    private void BtnConcurrencyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            if (int.TryParse(tag, out int value))
            {
                SliderConcurrency.Value = value;
                TxtConcurrencyValue.Text = value.ToString();
            }
        }
    }

    private void SliderRpm_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        var value = (int)e.NewValue;
        TxtRpmValue.Text = value.ToString();
    }

    private void BtnRpmPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            if (int.TryParse(tag, out int value))
            {
                SliderRpm.Value = value;
                TxtRpmValue.Text = value.ToString();
            }
        }
    }

    private void ResetToIdle()
    {
        _isWorking = false;
        BtnTranslate.Visibility = Visibility.Visible;
        BtnStop.Visibility = Visibility.Collapsed;
        StatsPanel.Visibility = Visibility.Collapsed;
        StatusIndicator.Fill = FindResource("TertiaryTextBrush") as Brush;
    }

    private const int LOG_MAX_CHARS = 60_000;
    private const int LOG_TRIM_TO_CHARS = 40_000;

    private void OnLogMessage(LogEntry entry)
    {
        // InvokeAsync（非阻塞）：翻译线程无需等待 UI 处理完成，高并发下不产生 Dispatcher 堆积。
        Dispatcher.InvokeAsync(() =>
        {
            var prefix = entry.Level switch
            {
                LogLevel.Error => "[ERROR] ",
                LogLevel.Warning => "[WARN]  ",
                LogLevel.Success => "[OK]    ",
                LogLevel.Debug => "[DEBUG] ",
                _ => "[INFO]  "
            };

            LogContent.Text += prefix + entry.Message + "\n";

            // 防止无限增长：超过上限时裁剪旧内容。
            if (LogContent.Text.Length > LOG_MAX_CHARS)
                LogContent.Text = LogContent.Text[^LOG_TRIM_TO_CHARS..];
        });
    }

    private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
    {
        if (_isWorking) return;
        _isWorking = true;

        var savedConfig = AppConfig.Load();
        _appConfig = savedConfig;
        LoadConfigToUI();
        var primary = _appConfig.GetPrimaryProvider();

        if (primary == null || string.IsNullOrWhiteSpace(primary.ApiKey))
        {
            ConsoleUtils.WriteError("请先在【配置】页面填写翻译服务 API Key");
            MessageBox.Show("请先在【配置】页面填写翻译服务 API Key", "配置不完整",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NavigateTo("config");
            ResetToIdle();
            return;
        }

        BtnTranslate.Visibility = Visibility.Collapsed;
        BtnStop.Visibility = Visibility.Visible;
        LogContent.Text = "";

        StatusText.Text = "正在准备...";
        StatusIndicator.Fill = FindResource("ElectricCyanBrush") as Brush;

        var dialog = new OpenFileDialog
        {
            Title = "选择游戏可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            ConsoleUtils.WriteWarning("未选择文件，操作取消。");
            ResetToIdle();
            StatusText.Text = "已取消";
            return;
        }

        _selectedExePath = dialog.FileName;
        ConsoleUtils.WriteInfo($"已选择: {_selectedExePath}");
        DiagnosticLogger.Log($"[MainWindow] 创建翻译代理, ExePath: {_selectedExePath}");

        _translationProxy = new TranslationProxyOptimized(
            _appConfig,
            maxConcurrency: _appConfig.MaxConcurrency,
            maxRpm: _appConfig.MaxRpm);
        _translationProxy.OnTranslation += OnProxyTranslation;
        _translationProxy.OnStatisticsChanged += OnProxyStatsChanged;
        _translationProxy.OnRpmChanged += OnProxyRpmChanged;

        if (!_translationProxy.Start())
        {
            ConsoleUtils.WriteError("翻译代理启动失败（端口 5588 可能被占用）");
            MessageBox.Show("翻译代理启动失败，请检查端口 5588 是否被占用", "启动失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
            _translationProxy = null;
            ResetToIdle();
            StatusText.Text = "代理启动失败";
            StatusIndicator.Fill = FindResource("ErrorBrush") as Brush;
            return;
        }

        await Task.Delay(300);
        var proxyOk = await CheckProxyHealthAsync();
        if (!proxyOk)
        {
            ConsoleUtils.WriteError("翻译代理健康检查失败");
            MessageBox.Show("代理健康检查失败，请检查 5588 端口是否被防火墙拦截", "健康检查失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _translationProxy.Dispose();
            _translationProxy = null;
            ResetToIdle();
            StatusText.Text = "健康检查失败";
            StatusIndicator.Fill = FindResource("ErrorBrush") as Brush;
            return;
        }
        ConsoleUtils.WriteSuccess("翻译代理已启动并验证通过 (127.0.0.1:5588)");

        var deployed = await Task.Run(() => DetectAndDeploy(_selectedExePath!));
        if (!deployed)
        {
            _translationProxy?.Dispose();
            _translationProxy = null;
            ResetToIdle();
            BtnTranslate.Visibility = Visibility.Visible;
            BtnStop.Visibility = Visibility.Collapsed;
            StatusText.Text = "部署失败";
            StatusIndicator.Fill = FindResource("ErrorBrush") as Brush;
            return;
        }

        await Task.Delay(500);

        ConsoleUtils.WriteInfo("正在启动游戏...");
        var launched = await Task.Run(() => LaunchGameProcess());
        if (!launched)
        {
            ConsoleUtils.WriteError("游戏启动失败");
            _translationProxy?.Dispose();
            _translationProxy = null;
            ResetToIdle();
            StatusText.Text = "游戏启动失败";
            StatusIndicator.Fill = FindResource("ErrorBrush") as Brush;
            return;
        }

        ConsoleUtils.WriteSuccess("部署完成！游戏已启动，翻译代理运行中...");
        StatusText.Text = "翻译代理运行中 | 127.0.0.1:5588";
        StatusIndicator.Fill = FindResource("SuccessBrush") as Brush;

        BtnTranslate.Visibility = Visibility.Collapsed;
        BtnStop.Visibility = Visibility.Visible;
        _isWorking = true;
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        bool hasProxy = _translationProxy != null;
        int totalRequests = 0, successCount = 0, failCount = 0, cacheHits = 0;
        double avgLatencyMs = 0;

        if (_translationProxy != null)
        {
            ConsoleUtils.WriteInfo("正在停止翻译代理...");
            totalRequests = _translationProxy.TotalRequests;
            successCount = _translationProxy.SuccessCount;
            failCount = _translationProxy.FailCount;
            cacheHits = _translationProxy.CacheHits;
            avgLatencyMs = _translationProxy.AverageLatencyMs;

            _translationProxy.Dispose();
            _translationProxy = null;
            ConsoleUtils.WriteSuccess("翻译代理已停止");
        }

        ResetToIdle();
        StatusText.Text = "已停止";
        StatusIndicator.Fill = FindResource("TertiaryTextBrush") as Brush;
        BtnTranslate.Visibility = Visibility.Visible;
        BtnStop.Visibility = Visibility.Collapsed;
        _isWorking = false;

        if (hasProxy)
        {
            var statsWindow = new StatsWindow
            {
                Owner = this
            };
            statsWindow.SetStats(totalRequests, successCount, failCount, cacheHits, avgLatencyMs);
            statsWindow.ShowDialog();
        }
    }

    private bool DetectAndDeploy(string exePath)
    {
        var gameDetector = new GameDetector();

        if (!gameDetector.ValidateGameExecutable(exePath))
        {
            ConsoleUtils.WriteError("无效的游戏可执行文件");
            return false;
        }

        ConsoleUtils.WriteInfo("正在检测游戏信息...");
        var gameInfo = gameDetector.DetectFromExecutable(exePath);
        _currentGame = gameInfo;

        ConsoleUtils.WriteInfo($"游戏: {gameInfo.GameName} ({gameInfo.Architecture} / {gameInfo.Type})");
        if (gameInfo.IsSteamGame)
        {
            var steamInfo = string.IsNullOrWhiteSpace(gameInfo.SteamAppId)
                ? "检测到 SteamAPI"
                : $"检测到 SteamAPI (AppID: {gameInfo.SteamAppId})";
            ConsoleUtils.WriteInfo(steamInfo);
        }
        if (!string.IsNullOrEmpty(gameInfo.UnityVersion) && gameInfo.UnityVersion != "Unknown")
            ConsoleUtils.WriteInfo($"Unity 版本: {gameInfo.UnityVersion}");

        _pluginDeployer = new PluginDeployer();

        if (gameInfo.IsBepInExInstalled)
        {
            ConsoleUtils.WriteWarning("检测到已安装 BepInEx，仅部署翻译组件");
            ConsoleUtils.WriteInfo($"正在部署 XUnity.AutoTranslator ({_pluginDeployer.CurrentVersion})...");
            _pluginDeployer.DeployXUnityOnly(gameInfo.GameRoot, gameInfo.Type, _pluginDeployer.CurrentVersion);
            ConsoleUtils.WriteInfo("正在部署 TMP 中文字体...");
            _pluginDeployer.DeployTmpFontAsset(gameInfo.GameRoot);
        }
        else
        {
            ConsoleUtils.WriteInfo($"正在部署 BepInEx + XUnity.AutoTranslator ({_pluginDeployer.CurrentVersion})...");

            var result = _pluginDeployer.Deploy(gameInfo, _pluginDeployer.CurrentVersion, false);

            if (result.Status == DeployStatus.Failed)
            {
                ConsoleUtils.WriteError(result.Message);
                return false;
            }

            ConsoleUtils.WriteSuccess(result.Message);
        }

        var primary = _appConfig.GetPrimaryProvider();
        var providerName = primary?.GetDisplayName() ?? "硅基流动";
        var modelName = primary?.GetModelDisplayName() ?? "DeepSeek-V4-Flash";

        ConsoleUtils.WriteInfo($"正在生成配置文件 ({providerName} AI 翻译)...");
        var configGen = new ConfigGenerator(_appConfig);
        configGen.GenerateConfigs(gameInfo.GameRoot, gameInfo);

        ConsoleUtils.WriteInfo($"翻译服务: {providerName} / {modelName}");
        ConsoleUtils.WriteInfo($"翻译端点: 本地代理 (127.0.0.1:5588 → {providerName} API)");

        var fallback = _appConfig.GetFallbackProvider();
        if (fallback != null)
        {
            ConsoleUtils.WriteInfo($"备用服务: {fallback.GetDisplayName()} / {fallback.GetModelDisplayName()}");
        }

        var logFileName = gameInfo.Type == GameType.IL2CPP ? "LogOutput.txt" : "LogOutput.log";
        ConsoleUtils.WriteInfo($"BepInEx 日志: {Path.Combine(gameInfo.GameRoot, "BepInEx", logFileName)}");

        // 验证 XUnity 插件是否已部署
        if (_pluginDeployer.FindXUnityAssembly(gameInfo.GameRoot, out var xunityPath, out var errorDetail))
        {
            ConsoleUtils.WriteSuccess($"XUnity 插件已部署: {xunityPath}");
        }
        else
        {
            ConsoleUtils.WriteError($"XUnity 插件部署验证失败: {errorDetail}");
            ConsoleUtils.WriteInfo("诊断指引:");
            ConsoleUtils.WriteInfo(_pluginDeployer.GetDiagnosticGuide(gameInfo));
        }

        ConsoleUtils.WriteSuccess("准备完成！");
        return true;
    }

    private bool LaunchGameProcess()
    {
        if (_currentGame == null) return false;

        try
        {
            var launcher = new GameLauncher();
            var process = launcher.LaunchGame(_currentGame);
            return process != null;
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"启动异常: {ex.Message}");
            return false;
        }
    }

    private void OnProxyTranslation(string sourceText, string translatedText, string? error)
    {
        Dispatcher.Invoke(() =>
        {
            var preview = sourceText.Length > 40 ? sourceText[..40] + "..." : sourceText;
            if (error != null)
            {
                ConsoleUtils.WriteError($"翻译失败: {preview} | {error}");
            }
            else
            {
                var resultPreview = translatedText.Length > 30 ? translatedText[..30] + "..." : translatedText;
                ConsoleUtils.WriteSuccess($"翻译: {preview} → {resultPreview}");
            }
        });
    }

    private void OnProxyStatsChanged(int total, int success, int fail)
    {
        Dispatcher.Invoke(() =>
        {
            StatsPanel.Visibility = Visibility.Visible;
            StatTotal.Text = total.ToString();
            StatSuccess.Text = success.ToString();
            StatFail.Text = fail.ToString();
            StatusText.Text = $"翻译代理运行中 | 总计: {total} | 成功: {success} | 失败: {fail}";
        });
    }

    private void OnProxyRpmChanged(int currentRpm, int maxRpm)
    {
        Dispatcher.Invoke(() =>
        {
            var ratio = maxRpm > 0 ? (double)currentRpm / maxRpm : 0;
            var color = ratio switch
            {
                < 0.6 => FindResource("SuccessBrush") as Brush,
                < 0.9 => FindResource("WarningBrush") as Brush,
                _ => FindResource("ErrorBrush") as Brush
            };
            StatRpm.Foreground = color;
            StatRpm.Text = $"{currentRpm}/{maxRpm}";
        });
    }

    private static async Task<bool> CheckProxyHealthAsync()
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync("http://127.0.0.1:5588/health");
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content.ReadAsStringAsync();
            return body == "OK";
        }
        catch
        {
            return false;
        }
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var provider = ComboBoxIndexToProvider(CmbProvider.SelectedIndex);
        var modelName = GetSelectedModelName(provider, CmbModel);

        var providers = new List<ProviderConfig>();

        providers.Add(new ProviderConfig
        {
            Provider = provider,
            ApiKey = TxtApiKey.Password.Trim(),
            SecretKey = ProviderFactory.RequiresSecretKey(provider) ? TxtSecretKey.Password.Trim() : null,
            ModelName = modelName,
            IsPrimary = true
        });

        var fallbackProvider = FallbackComboBoxIndexToProvider(CmbFallbackProvider.SelectedIndex);
        if (fallbackProvider != null && !string.IsNullOrWhiteSpace(TxtFallbackApiKey.Password.Trim()))
        {
            var fallbackModelName = GetSelectedModelName(fallbackProvider.Value, CmbFallbackModel);
            providers.Add(new ProviderConfig
            {
                Provider = fallbackProvider.Value,
                ApiKey = TxtFallbackApiKey.Password.Trim(),
                SecretKey = ProviderFactory.RequiresSecretKey(fallbackProvider.Value) ? TxtFallbackSecretKey.Password.Trim() : null,
                ModelName = fallbackModelName,
                IsPrimary = false
            });
        }

        _appConfig.Providers = providers;
        _appConfig.MaxConcurrency = (int)SliderConcurrency.Value;
        _appConfig.MaxRpm = (int)SliderRpm.Value;
        _appConfig.BatchModeEnabled = ChkBatchEnabled.IsChecked ?? true;
        _appConfig.BatchSize = (int)SliderBatchSize.Value;
        _appConfig.SourceLanguage = AppConfig.IndexToSourceLanguage(CmbSourceLang.SelectedIndex);
        _appConfig.TargetLanguage = AppConfig.IndexToTargetLanguage(CmbTargetLang.SelectedIndex);

        _appConfig.Save();

        var primary = _appConfig.GetPrimaryProvider();
        var displayName = primary?.GetDisplayName() ?? "Unknown";
        var batchInfo = _appConfig.BatchModeEnabled ? $"批量: {_appConfig.BatchSize}条" : "逐条";
        LblConfigStatus.Text = $"✓ 配置已保存！服务: {displayName}, 并发: {_appConfig.MaxConcurrency}, RPM: {_appConfig.MaxRpm}, {batchInfo}, 语言: {_appConfig.SourceLanguage}→{_appConfig.TargetLanguage}";
        LblConfigStatus.Foreground = FindResource("SuccessBrush") as Brush;

        if (primary == null || string.IsNullOrWhiteSpace(primary.ApiKey))
        {
            LblConfigStatus.Text = "⚠ 请填写 API Key 后再使用";
            LblConfigStatus.Foreground = FindResource("WarningBrush") as Brush;
        }
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        var provider = ComboBoxIndexToProvider(CmbProvider.SelectedIndex);
        var providerName = ProviderComboBoxNames.GetValueOrDefault(provider, provider.ToString());
        LblConfigStatus.Text = $"⏳ 正在测试 {providerName} 连接...";
        LblConfigStatus.Foreground = FindResource("InfoBrush") as Brush;

        var apiKey = TxtApiKey.Password.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LblConfigStatus.Text = "✗ 请先填写 API Key";
            LblConfigStatus.Foreground = FindResource("ErrorBrush") as Brush;
            return;
        }

        var modelName = GetSelectedModelName(provider, CmbModel);

        try
        {
            ProviderHealthCheckResult result;
            using (var modelProvider = ProviderFactory.Create(new ProviderConfig
            {
                Provider = provider,
                ApiKey = apiKey,
                SecretKey = TxtSecretKey?.Password.Trim() ?? "",
                ModelName = modelName
            }))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                result = await modelProvider.HealthCheckAsync(cts.Token);
            }

            if (result.IsHealthy)
            {
                LblConfigStatus.Text = $"✓ {providerName} 连接成功！{result.Message}";
                LblConfigStatus.Foreground = FindResource("SuccessBrush") as Brush;
            }
            else
            {
                LblConfigStatus.Text = $"✗ {providerName} 连接失败: {result.Message}";
                LblConfigStatus.Foreground = FindResource("ErrorBrush") as Brush;
            }
        }
        catch (OperationCanceledException)
        {
            LblConfigStatus.Text = "✗ 连接超时: 请检查网络";
            LblConfigStatus.Foreground = FindResource("ErrorBrush") as Brush;
        }
        catch (Exception ex)
        {
            LblConfigStatus.Text = $"✗ 网络错误: {ex.Message}";
            LblConfigStatus.Foreground = FindResource("ErrorBrush") as Brush;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ConsoleUtils.OnLog -= OnLogMessage;
        _translationProxy?.Dispose();
        base.OnClosed(e);
    }

    private void BtnCopyInviteCode_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TxtInviteCode.Text);
        LblConfigStatus.Text = "✓ 邀请码已复制到剪贴板";
        LblConfigStatus.Foreground = FindResource("SuccessBrush") as Brush;
    }
}
