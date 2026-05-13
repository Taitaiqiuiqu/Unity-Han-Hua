using OneClickChineseMod.Utils;

namespace OneClickChineseMod.Commands;

public class HelpCommand
{
    public void Execute()
    {
        Console.WriteLine($"{AppBasicInfoManager.AppDisplayName} - {AppBasicInfoManager.CliDescription}");
        Console.WriteLine();
        Console.WriteLine("本工具可为 Unity 游戏自动安装 BepInEx + XUnity.AutoTranslator 翻译插件，");
        Console.WriteLine("实现游戏文本的自动汉化。");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 快速开始 ===");
        Console.WriteLine("  方式一：直接双击运行程序，在弹出的对话框中选择游戏 .exe 文件");
        Console.WriteLine("  方式二：打开命令行，运行：");
        Console.WriteLine($"    SenGameLoc.exe install \"D:\\Games\\MyGame\\Game.exe\"");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 命令 ===");
        Console.WriteLine("  install <game.exe>  安装翻译插件到指定游戏");
        Console.WriteLine("  help                显示帮助信息");
        Console.WriteLine("  version             显示版本信息");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 安装选项 ===");
        Console.WriteLine("  --force, -f         强制覆盖已安装的插件");
        Console.WriteLine("  --skip-launch       安装完成后不启动游戏");
        Console.WriteLine("  --dry-run           模拟运行，不实际修改任何文件");
        Console.WriteLine("  --verbose, -v       显示详细日志（调试用）");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 检测能力 ===");
        Console.WriteLine("  自动识别游戏架构: x86 (32位) / x64 (64位)");
        Console.WriteLine("  自动识别游戏类型: Mono / IL2CPP");
        Console.WriteLine("  自动匹配 BepInEx 版本: 5.4.x (Mono) / 6.0.x (IL2CPP)");
        Console.WriteLine("  自动检测 Unity 版本");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 安装内容 ===");
        Console.WriteLine("  BepInEx 插件框架 → BepInEx/core/ 和游戏根目录");
        Console.WriteLine("  XUnity.AutoTranslator 翻译插件 → BepInEx/plugins/");
        Console.WriteLine("  AutoTranslator 配置文件 → BepInEx/config/AutoTranslatorConfig.ini");
        Console.WriteLine("  doorstop 配置 → 游戏根目录");
        Console.WriteLine();
        ConsoleUtils.WriteInfo("=== 常见问题 ===");
        Console.WriteLine("  Q: 安装后游戏没有翻译？");
        Console.WriteLine("  A: 请确保游戏安装路径不包含中文特殊字符，并检查 BepInEx/config/AutoTranslatorConfig.ini");
        Console.WriteLine();
        Console.WriteLine("  Q: 如何卸载？");
        Console.WriteLine("  A: 删除游戏目录下的 BepInEx 文件夹、doorstop*.ini、winhttp.dll 即可");
    }
}
