# Resources 目录说明

本目录需要放置以下资源文件，这些文件因为体积较大或需要从外部获取，不直接提交到Git仓库。

## 必需文件

### BepInEx 插件加载器
- **BepInEx_win_x64_5.4.23.4.zip** 或 **BepInEx_win_x64_5.4.23.5.zip** (64位)
- **BepInEx_win_x86_5.4.23.4.zip** 或 **BepInEx_win_x86_5.4.23.5.zip** (32位)
- **BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a.zip** (IL2CPP 64位)
- **BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.755+3fab71a.zip** (IL2CPP 32位)

下载地址：https://github.com/BepInEx/BepInEx/releases

### XUnity 自动翻译插件
- **XUnity.AutoTranslator-BepInEx-5.4.4.zip**
- **XUnity.AutoTranslator-BepInEx-5.5.0.zip**
- **XUnity.AutoTranslator-BepInEx-5.5.2.zip**
- **XUnity.AutoTranslator-BepInEx-5.6.1.zip**
- **XUnity.AutoTranslator-BepInEx-IL2CPP-5.4.4.zip** (IL2CPP版本)
- **XUnity.AutoTranslator-BepInEx-IL2CPP-5.5.0.zip** (IL2CPP版本)
- **XUnity.AutoTranslator-BepInEx-IL2CPP-5.5.2.zip** (IL2CPP版本)
- **XUnity.AutoTranslator-BepInEx-IL2CPP-5.6.1.zip** (IL2CPP版本)

下载地址：https://github.com/AndroidShadow77/XUnity.AutoTranslator/releases

### 字体资源
- **TMP_Font_AssetBundles.zip** - TextMeshPro字体资源包

### 翻译插件
- **DeepSeekTranslate.dll** - DeepSeek翻译接口DLL（需要自行编译或获取）

## 目录用途

这些资源文件用于：
1. 自动检测并安装BepInEx插件加载器到游戏目录
2. 自动部署XUnity自动翻译插件
3. 提供中文字体支持
4. 提供DeepSeek API翻译功能
