# trantion - AI 游戏翻译工具

一个基于 DeepSeek AI 的 Unity 游戏一键汉化工具。

## 功能特性

- ⚡ **一键部署**：自动检测游戏类型，智能安装所需组件
- 🤖 **DeepSeek AI 驱动**：使用 DeepSeek V4 Flash/Pro 模型进行高质量翻译
- 🎮 **多游戏支持**：兼容 Mono 和 IL2CPP 类型的 Unity 游戏
- 💾 **配置持久化**：API Key 和模型设置自动保存

## 系统要求

- Windows 10/11 (x64)
- .NET 9.0 Runtime（如果使用非自包含版本）
- DeepSeek API Key
- 网络连接

## 使用方法

### 1. 配置 API

1. 获取 DeepSeek API Key：https://platform.deepseek.com/api_keys
2. 运行 trantion.exe
3. 切换到【配置】页面
4. 填写 API Key
5. 选择翻译模型：
   - **DeepSeek-V4-Flash**：快速、便宜，适合大多数场景
   - **DeepSeek-V4-Pro**：精准推理，适合高质量需求
6. 点击【保存配置】

### 2. 一键汉化

1. 在【一键汉化】页面点击【⚡ 一键汉化】按钮
2. 选择游戏的 .exe 文件
3. 工具会自动：
   - 检测游戏类型（Mono / IL2CPP）
   - 部署 BepInEx 插件加载器
   - 安装 XUnity.AutoTranslator 翻译插件
   - 配置 DeepSeek API 端点
   - 安装中文字体
4. 自动启动游戏，翻译即时生效

### 3. 查看日志

BepInEx 日志文件位置：
- Mono 游戏：`游戏目录\BepInEx\LogOutput.log`
- IL2CPP 游戏：`游戏目录\BepInEx\LogOutput.txt`

## 工作原理

```
游戏文本 → XUnity.AutoTranslator → DeepSeekTranslate.dll → DeepSeek API → 翻译结果 → 游戏界面
```

1. XUnity.AutoTranslator 拦截游戏中的文本
2. 将文本发送给 DeepSeekTranslate.dll
3. DeepSeekTranslate.dll 调用 DeepSeek API 进行翻译
4. 翻译结果实时显示在游戏中

## 项目结构

```
src/OneClickChineseMod/
├── Commands/          # 命令处理
├── Core/              # 核心功能
│   ├── ConfigGenerator.cs     # 生成游戏端配置文件
│   ├── GameDetector.cs        # 游戏类型检测
│   ├── GameLauncher.cs        # 游戏启动
│   └── PluginDeployer.cs      # 插件部署
├── Models/            # 数据模型
├── Utils/             # 工具类
├── Resources/        # 资源文件（需自行准备）
├── MainForm.cs       # 主界面
└── Program.cs        # 程序入口
```

## 编译项目

```bash
cd src/OneClickChineseMod
dotnet build -c Release
```

发布单文件：
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## 注意事项

- 请勿删除游戏目录下的 BepInEx 文件夹
- 进入游戏后稍等片刻，翻译会自动加载
- 如遇问题，请检查 BepInEx 日志文件
- 确保 DeepSeek API 账户有足够余额
