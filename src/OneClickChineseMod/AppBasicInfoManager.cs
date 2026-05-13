namespace OneClickChineseMod;

public static class AppBasicInfoManager
{
    // ── 窗口标题栏 ──
    // 显示位置: MainWindow.xaml 窗口 Title 属性
    // 用途说明: 应用程序在操作系统任务栏和窗口标题栏上显示的名称
    public const string AppWindowTitle = "张先森汉化工具";

    // ── Header 导航栏 ──
    // 显示位置: MainWindow.xaml 顶部 Header 栏左侧 Logo 旁
    // 用途说明: 应用程序的显示名称，用于导航栏和应用内主要场景
    public const string AppDisplayName = "张先森汉化工具";

    // ── 首页 Hero 区域 ──
    // 显示位置: MainWindow.xaml 首页 (HomePage) 大标题
    // 显示位置: MainWindow.xaml 首页 (HomePage) 副标题
    // 用途说明: 首页核心区域的主标题和副标题，用于第一时间向用户传达产品定位
    public const string AppSubtitle = "张先森汉化工具";

    // ── 首页描述文本 ──
    // 显示位置: MainWindow.xaml 首页 (HomePage) Hero 下方的功能介绍
    // 用途说明: 详细描述应用程序的核心功能和优势，帮助用户快速了解产品能力
    public const string Description =
        "免费，高效的 Unity 游戏汉化工具，自动检测游戏类型、部署翻译插件、启动 AI 翻译服务，让游戏汉化变得简单。";

    // ── 关于页面 ──
    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 顶部大标题 / 版本号
    // 用途说明: 标识当前应用程序的版本号，用于 About 页面顶部展示
    public const string AppVersion = "v1.0.0";

    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 版本号下方的介绍文字
    // 用途说明: 向用户简要介绍应用程序的核心定位和价值主张
    public const string AboutDescription =
        "一款基于 AI 的 Unity 游戏汉化工具，为游戏玩家提供便捷的自动翻译体验。";

    // ── 关于页面 - 卡片标题 ──
    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 三个卡片的标题
    // 用途说明: 三个功能卡片的导航标题，帮助用户快速了解页面内容板块
    public const string CardTitleAboutUs = "关于我们";
    public const string CardTitleTechStack = "技术栈";
    public const string CardTitleChangelog = "更新日志";

    // ── 关于页面 - 「关于我们」卡片 ──
    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 「关于我们」卡片
    // 用途说明: 展示应用程序的创始人信息
    public const string Founder = "张先森";

    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) QQ群信息展示
    // 用途说明: 提供用户加入QQ群聊的入口，便于获取最新版本和社区支持
    public const string QQGroupNumber = "1078259906";
    public const string QQGroupDescription = "交流群 · 问题反馈 · 版本首发";

    // ── 关于页面 - 「技术栈」卡片 ──
    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 「技术栈」卡片每一行右侧信息
    // 用途说明: 展示应用程序所使用的核心技术框架、服务和插件，便于用户了解技术实现
    public const string TechFrontend = "WPF (.NET 9)";
    public const string TechAIService = "硅基流动 (DeepSeek V4)";
    public const string TechTranslatePlugin = "XUnity.AutoTranslator";
    public const string TechPluginFramework = "BepInEx 5.x / 6.x";

    // ── 关于页面 - 「更新日志」卡片 ──
    // 显示位置: MainWindow.xaml 关于页面 (AboutPage) 「更新日志」卡片
    // 用途说明: 记录应用程序的版本迭代历史，让用户了解功能演进和修复内容
    public const string ChangelogV100 = "v1.0.0 (2026-05-06) - 首个版本发布";
    public const string ChangelogV100Item1 = "支持 Mono / IL2CPP 游戏自动检测";
    public const string ChangelogV100Item2 = "集成主流国内模型";

    // ── 底部 Footer 栏 ──
    // 显示位置: MainWindow.xaml 窗口底部 Footer
    // 用途说明: 应用程序底部的标语展示，中英双语呈现产品名称和定位
    public const string AppFooter = "张先森汉化工具 - AI Game Translation Tool";

    // ── 命令行界面 ──
    // 显示位置: HelpCommand.cs / InstallCommand.cs 帮助信息标题
    // 用途说明: 命令行工具的帮助信息描述，用于 -h / --help 输出
    public const string CliDescription = "一键汉化工具";

    // 显示位置: HelpCommand.cs / InstallCommand.cs 用法示例行
    // 用途说明: 命令行工具的标准用法格式示例，帮助用户了解正确调用方式
    public const string CliUsage = "用法: SenGameLoc.exe install <game.exe> [选项]";

    // ── 内部标识 ──
    // 用途: 日志输出、配置目录名称等英文环境场景
    // 用途说明: 应用程序的英文标识名称，用于日志文件、配置目录等英文场景
    public const string AppEnglishName = "SenGameLoc";
}
