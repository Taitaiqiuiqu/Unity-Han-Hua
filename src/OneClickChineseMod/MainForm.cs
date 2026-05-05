using System.Diagnostics;
using OneClickChineseMod.Core;
using OneClickChineseMod.Models;
using OneClickChineseMod.Utils;

namespace OneClickChineseMod;

public class MainForm : Form
{
    private AppConfig _appConfig;
    private PluginDeployer? _pluginDeployer;

    private TabControl _tabControl = null!;
    private TabPage _tabMod = null!;
    private TabPage _tabConfig = null!;

    private RichTextBox _logBox = null!;
    private Label _promptLabel = null!;
    private Button _btnMod = null!;
    private Button _btnRetry = null!;

    private TextBox _txtApiKey = null!;
    private ComboBox _cmbModel = null!;
    private Button _btnSaveConfig = null!;
    private Button _btnTestConn = null!;
    private Label _lblConfigStatus = null!;

    private GameInfo? _currentGame;
    private string? _selectedExePath;
    private bool _isWorking;

    public MainForm()
    {
        _appConfig = AppConfig.Load();
        InitializeComponent();
        ConsoleUtils.OnLog += OnLogMessage;
        LoadConfigToUI();
        ResetToIdle();
    }

    private void InitializeComponent()
    {
        Text = "trantion - AI 游戏翻译工具";
        Size = new Size(640, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Microsoft YaHei UI", 10f);

        _tabControl = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(608, 570)
        };
        Controls.Add(_tabControl);

        BuildModTab();
        BuildConfigTab();
    }

    private void BuildModTab()
    {
        _tabMod = new TabPage("⚡ 一键汉化");
        _tabMod.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);

        _logBox = new RichTextBox
        {
            Location = new Point(16, 16),
            Size = new Size(570, 330),
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(200, 200, 200),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        _tabMod.Controls.Add(_logBox);

        _promptLabel = new Label
        {
            Location = new Point(16, 356),
            Size = new Size(570, 45),
            Text = "欢迎使用 trantion！DeepSeek AI 驱动翻译。\n①【配置】页填写 API Key → ② 点击下方按钮 → ③ 选游戏 exe → ④ 自动完成",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 10f),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        _tabMod.Controls.Add(_promptLabel);

        _btnRetry = CreateButton("重试安装", Color.FromArgb(244, 67, 54));
        _btnRetry.Location = new Point(178, 410);
        _btnRetry.Visible = false;
        _btnRetry.Click += OnRetryClick;
        _tabMod.Controls.Add(_btnRetry);

        _btnMod = new Button
        {
            Text = "⚡ 一键汉化",
            Size = new Size(260, 44),
            Location = new Point(190, 410),
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnMod.FlatAppearance.BorderSize = 0;
        _btnMod.Click += OnModClick;
        _tabMod.Controls.Add(_btnMod);

        _tabControl.TabPages.Add(_tabMod);
    }

    private void BuildConfigTab()
    {
        _tabConfig = new TabPage("⚙ 配置");
        _tabConfig.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);

        var leftX = 60;
        var fieldW = 460;
        var labelW = 140;
        var y = 40;
        var dy = 56;

        var header = new Label
        {
            Text = "DeepSeek AI 翻译配置",
            Location = new Point(leftX + labelW, y - 30),
            Size = new Size(300, 24),
            Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        _tabConfig.Controls.Add(header);

        AddConfigLabel("API Key:", leftX, y);
        _txtApiKey = new TextBox
        {
            Location = new Point(leftX + labelW, y),
            Size = new Size(fieldW, 32),
            Font = new Font("Consolas", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            PasswordChar = '•'
        };
        _tabConfig.Controls.Add(_txtApiKey);

        y += dy;
        AddConfigLabel("模型:", leftX, y);
        _cmbModel = new ComboBox
        {
            Location = new Point(leftX + labelW, y),
            Size = new Size(300, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        _cmbModel.Items.AddRange(new[] { "DeepSeek-V4-Flash (快速，便宜)", "DeepSeek-V4-Pro (精准推理)" });
        _tabConfig.Controls.Add(_cmbModel);

        y += dy + 20;

        var btnPanel = new Panel
        {
            Location = new Point(leftX + labelW, y),
            Size = new Size(360, 50)
        };
        _tabConfig.Controls.Add(btnPanel);

        _btnSaveConfig = new Button
        {
            Text = "💾 保存配置",
            Size = new Size(160, 44),
            Location = new Point(0, 0),
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSaveConfig.FlatAppearance.BorderSize = 0;
        _btnSaveConfig.Click += OnSaveConfigClick;
        btnPanel.Controls.Add(_btnSaveConfig);

        _btnTestConn = new Button
        {
            Text = "🔗 测试连接",
            Size = new Size(160, 44),
            Location = new Point(180, 0),
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(255, 152, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnTestConn.FlatAppearance.BorderSize = 0;
        _btnTestConn.Click += OnTestConnectionClick;
        btnPanel.Controls.Add(_btnTestConn);

        _lblConfigStatus = new Label
        {
            Location = new Point(leftX + labelW + 30, y + 50),
            Size = new Size(300, 24),
            Font = new Font("Microsoft YaHei UI", 9f),
            ForeColor = Color.FromArgb(46, 125, 50)
        };
        _tabConfig.Controls.Add(_lblConfigStatus);

        y += dy + 90;
        var hint = new Label
        {
            Text = "DeepSeek API 申请: https://platform.deepseek.com/api_keys\n\n翻译流程: XUnity Hook 游戏文本 → DeepSeekTranslate 直接调用 DeepSeek API\n无需本地代理，API Key 写入游戏配置，启动即翻译。",
            Location = new Point(leftX, y),
            Size = new Size(510, 120),
            Font = new Font("Microsoft YaHei UI", 8.5f),
            ForeColor = Color.FromArgb(120, 120, 120)
        };
        _tabConfig.Controls.Add(hint);

        _tabControl.TabPages.Add(_tabConfig);
    }

    private void AddConfigLabel(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(135, 32),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Microsoft YaHei UI", 10f)
        };
        _tabConfig.Controls.Add(label);
    }

    private Button CreateButton(string text, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(280, 44),
            Visible = false,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void LoadConfigToUI()
    {
        _txtApiKey.Text = _appConfig.DeepSeekApiKey;
        _cmbModel.SelectedIndex = _appConfig.DeepSeekModel == DeepSeekModel.V4Flash ? 0 : 1;
    }

    private void OnSaveConfigClick(object? sender, EventArgs e)
    {
        _appConfig.DeepSeekApiKey = _txtApiKey.Text.Trim();
        _appConfig.DeepSeekModel = _cmbModel.SelectedIndex == 0
            ? DeepSeekModel.V4Flash : DeepSeekModel.V4Pro;
        _appConfig.Save();

        _lblConfigStatus.Text = "✅ 配置已保存！";
        _lblConfigStatus.ForeColor = Color.FromArgb(46, 125, 50);

        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += (_, _) => { _lblConfigStatus.Text = ""; timer.Stop(); timer.Dispose(); };
        timer.Start();

        if (string.IsNullOrWhiteSpace(_appConfig.DeepSeekApiKey))
        {
            _lblConfigStatus.Text = "⚠ 请填写 API Key 后再使用";
            _lblConfigStatus.ForeColor = Color.FromArgb(200, 100, 0);
        }
    }

    private async void OnTestConnectionClick(object? sender, EventArgs e)
    {
        _btnSaveConfig.Enabled = false;
        _btnTestConn.Enabled = false;
        _lblConfigStatus.Text = "⏳ 正在测试 DeepSeek API 连接...";
        _lblConfigStatus.ForeColor = Color.FromArgb(0, 120, 212);

        var apiKey = _txtApiKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _lblConfigStatus.Text = "❌ 请先填写 API Key";
            _lblConfigStatus.ForeColor = Color.FromArgb(200, 50, 50);
            _btnSaveConfig.Enabled = true;
            _btnTestConn.Enabled = true;
            return;
        }

        var model = _cmbModel.SelectedIndex == 0 ? "deepseek-chat" : "deepseek-reasoner";

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                max_tokens = 10,
                temperature = 0
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _lblConfigStatus.Text = "✅ DeepSeek API 连接成功！";
                _lblConfigStatus.ForeColor = Color.FromArgb(46, 125, 50);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var statusCode = (int)response.StatusCode;
                if (statusCode == 401)
                    _lblConfigStatus.Text = "❌ API Key 无效 (401)";
                else if (statusCode == 403)
                    _lblConfigStatus.Text = "❌ 访问被拒 (403): 请检查账户余额";
                else
                    _lblConfigStatus.Text = $"❌ API 错误 ({statusCode})";
                _lblConfigStatus.ForeColor = Color.FromArgb(200, 50, 50);
            }
        }
        catch (TaskCanceledException)
        {
            _lblConfigStatus.Text = "❌ 连接超时: 请检查网络";
            _lblConfigStatus.ForeColor = Color.FromArgb(200, 50, 50);
        }
        catch (Exception ex)
        {
            _lblConfigStatus.Text = $"❌ 网络错误: {ex.Message}";
            _lblConfigStatus.ForeColor = Color.FromArgb(200, 50, 50);
        }

        _btnSaveConfig.Enabled = true;
        _btnTestConn.Enabled = true;
    }

    private void ResetToIdle()
    {
        _isWorking = false;
        _btnMod.Enabled = true;
        _btnMod.Visible = true;
        _btnRetry.Visible = false;
    }

    private void OnLogMessage(LogEntry entry)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnLogMessage(entry));
            return;
        }

        var color = entry.Level switch
        {
            LogLevel.Error => Color.FromArgb(255, 100, 100),
            LogLevel.Warning => Color.FromArgb(255, 200, 100),
            LogLevel.Success => Color.FromArgb(100, 255, 100),
            LogLevel.Debug => Color.FromArgb(150, 150, 150),
            _ => Color.FromArgb(200, 200, 200)
        };

        var prefix = entry.Level switch
        {
            LogLevel.Error => " [ERROR] ",
            LogLevel.Warning => " [WARN]  ",
            LogLevel.Success => " [OK]    ",
            LogLevel.Debug => " [DEBUG] ",
            _ => " [INFO]  "
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText(prefix + entry.Message + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private async void OnModClick(object? sender, EventArgs e)
    {
        if (_isWorking) return;
        _isWorking = true;

        _appConfig = AppConfig.Load();

        if (string.IsNullOrWhiteSpace(_appConfig.DeepSeekApiKey))
        {
            ConsoleUtils.WriteError("请先在【配置】页面填写 DeepSeek API Key");
            _promptLabel.ForeColor = Color.FromArgb(200, 100, 0);
            _promptLabel.Text = "⚠ 请先前往【配置】页面填写 DeepSeek API Key";
            ResetToIdle();
            return;
        }

        _btnMod.Enabled = false;
        _btnMod.Visible = true;
        _btnRetry.Visible = false;
        _logBox.Clear();

        _promptLabel.ForeColor = Color.FromArgb(0, 120, 212);
        _promptLabel.Text = "正在准备...";

        var dialog = new OpenFileDialog
        {
            Title = "选择游戏可执行文件",
            Filter = "可执行文件 (*.exe)|*.exe",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            ConsoleUtils.WriteWarning("未选择文件，操作取消。");
            _promptLabel.Text = "已取消。欢迎使用 trantion！";
            ResetToIdle();
            return;
        }

        _selectedExePath = dialog.FileName;
        ConsoleUtils.WriteInfo($"已选择: {_selectedExePath}");
        ConsoleUtils.WriteLine();

        var deployed = await Task.Run(() => DetectAndDeploy(_selectedExePath));
        if (!deployed)
        {
            ConsoleUtils.WriteError("部署失败，请检查日志获取详细信息。");
            _promptLabel.ForeColor = Color.FromArgb(200, 50, 50);
            _promptLabel.Text = "部署失败，请检查日志。";
            _btnRetry.Visible = true;
            _btnMod.Enabled = true;
            return;
        }

        await Task.Delay(500);

        ConsoleUtils.WriteLine();
        ConsoleUtils.WriteInfo("正在启动游戏...");
        ConsoleUtils.WriteLine();

        var launched = await Task.Run(() => LaunchGameProcess());
        if (!launched)
        {
            ConsoleUtils.WriteError("游戏启动失败，请检查游戏路径。");
            _promptLabel.ForeColor = Color.FromArgb(200, 50, 50);
            _promptLabel.Text = "游戏启动失败，请重试。";
            _btnRetry.Visible = true;
            _btnMod.Enabled = true;
            return;
        }

        ConsoleUtils.WriteSuccess("部署完成！游戏已启动，翻译由 DeepSeek AI 自动处理。");
        ConsoleUtils.WriteInfo("提示：进入游戏后稍等片刻，文本将自动翻译为中文。");
        ConsoleUtils.WriteInfo("请勿删除游戏目录下的 BepInEx 文件夹。");

        _promptLabel.ForeColor = Color.FromArgb(46, 125, 50);
        _promptLabel.Text = "部署完成！游戏已启动，翻译由 DeepSeek AI 自动处理。";
        ResetToIdle();
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
        if (!string.IsNullOrEmpty(gameInfo.UnityVersion) && gameInfo.UnityVersion != "Unknown")
            ConsoleUtils.WriteInfo($"Unity 版本: {gameInfo.UnityVersion}");

        _pluginDeployer = new PluginDeployer();

        if (gameInfo.IsBepInExInstalled)
        {
            ConsoleUtils.WriteWarning("检测到已安装 BepInEx，仅部署翻译组件");
            ConsoleUtils.WriteInfo($"正在部署 XUnity.AutoTranslator ({_pluginDeployer.CurrentVersion})...");
            _pluginDeployer.DeployXUnityOnly(gameInfo.GameRoot, gameInfo.Type, _pluginDeployer.CurrentVersion);
            ConsoleUtils.WriteInfo("正在部署 DeepSeekTranslate 端点...");
            _pluginDeployer.DeployDeepSeekTranslate(gameInfo.GameRoot);
            ConsoleUtils.WriteInfo("正在部署 TMP 中文字体...");
            _pluginDeployer.DeployTmpFontAsset(gameInfo.GameRoot);
            ConsoleUtils.WriteLine();
        }
        else
        {
            ConsoleUtils.WriteLine();
            ConsoleUtils.WriteInfo($"正在部署 BepInEx + XUnity.AutoTranslator ({_pluginDeployer.CurrentVersion}) + DeepSeekTranslate...");

            var result = _pluginDeployer.Deploy(gameInfo, _pluginDeployer.CurrentVersion, false);

            if (result.Status == DeployStatus.Failed)
            {
                ConsoleUtils.WriteError(result.Message);
                return false;
            }

            ConsoleUtils.WriteSuccess(result.Message);
        }

        ConsoleUtils.WriteInfo("正在生成配置文件 (DeepSeek AI 翻译)...");
        var configGen = new ConfigGenerator(_appConfig);
        configGen.GenerateConfigs(gameInfo.GameRoot, gameInfo);

        ConsoleUtils.WriteInfo($"翻译模型: {_appConfig.GetModelName()}");
        ConsoleUtils.WriteInfo($"XUnity 版本: {_pluginDeployer.CurrentVersion}");
        ConsoleUtils.WriteInfo($"翻译端点: DeepSeek API (内嵌)");

        // IL2CPP 游戏使用 BepInEx 6，日志文件为 .txt；Mono 游戏使用 BepInEx 5，日志文件为 .log
        var logFileName = gameInfo.Type == GameType.IL2CPP ? "LogOutput.txt" : "LogOutput.log";
        ConsoleUtils.WriteInfo($"BepInEx 日志: {Path.Combine(gameInfo.GameRoot, "BepInEx", logFileName)}");
        ConsoleUtils.WriteLine();
        ConsoleUtils.WriteSuccess("准备完成！");
        return true;
    }

    private bool LaunchGameProcess()
    {
        if (_currentGame == null) return false;

        var fullExePath = Path.Combine(_currentGame.GameRoot, _currentGame.GameName + ".exe");
        if (!File.Exists(fullExePath))
            fullExePath = _currentGame.GamePath;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fullExePath,
                WorkingDirectory = _currentGame.GameRoot,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError($"启动异常: {ex.Message}");
            return false;
        }
    }

    private void OnRetryClick(object? sender, EventArgs e)
    {
        _btnRetry.Visible = false;
        _btnMod.Enabled = true;
        _btnMod.Visible = true;
        _isWorking = false;
        _promptLabel.ForeColor = Color.FromArgb(0, 120, 212);
        _promptLabel.Text = "请重试。如持续失败请检查：① 网络连接 ② API Key ③ 关闭杀毒软件。";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ConsoleUtils.OnLog -= OnLogMessage;
        }
        base.Dispose(disposing);
    }
}
