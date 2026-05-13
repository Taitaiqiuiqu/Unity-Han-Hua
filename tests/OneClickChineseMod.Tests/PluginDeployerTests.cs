using OneClickChineseMod.Core;
using OneClickChineseMod.Models;

namespace OneClickChineseMod.Tests;

public class PluginDeployerTests
{
    [Fact]
    public void ValidateInstallation_WithAllRequiredMonoFiles_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(tempDir, "BepInEx", "core", "BepInEx.dll"), "dummy");
        File.WriteAllText(Path.Combine(tempDir, "winhttp.dll"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.Mono);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithAllRequiredIl2CppFiles_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(tempDir, "BepInEx", "core", "BepInEx.dll"), "dummy");
        File.WriteAllText(Path.Combine(tempDir, "doorstop_config.ini"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.IL2CPP);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithMissingBepInExDll_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(tempDir, "winhttp.dll"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.Mono);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithMissingPluginsDirectory_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "core"));
        File.WriteAllText(Path.Combine(tempDir, "BepInEx", "core", "BepInEx.dll"), "dummy");
        File.WriteAllText(Path.Combine(tempDir, "winhttp.dll"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.Mono);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithMissingWinhttp_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(tempDir, "BepInEx", "core", "BepInEx.dll"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.Mono);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithMissingDoorstopConfig_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(tempDir, "BepInEx", "core", "BepInEx.dll"), "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.ValidateInstallation(tempDir, GameType.IL2CPP);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateInstallation_WithEmptyDirectory_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var deployer = new PluginDeployer();
            var resultMono = deployer.ValidateInstallation(tempDir, GameType.Mono);
            var resultIl2Cpp = deployer.ValidateInstallation(tempDir, GameType.IL2CPP);

            Assert.False(resultMono);
            Assert.False(resultIl2Cpp);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AvailableVersions_ContainsAllExpectedVersions()
    {
        Assert.Equal(4, PluginDeployer.AvailableVersions.Length);
        Assert.Contains(XUnityVersion.V5_4_4, PluginDeployer.AvailableVersions);
        Assert.Contains(XUnityVersion.V5_5_0, PluginDeployer.AvailableVersions);
        Assert.Contains(XUnityVersion.V5_5_2, PluginDeployer.AvailableVersions);
        Assert.Contains(XUnityVersion.V5_6_1, PluginDeployer.AvailableVersions);
    }

    [Fact]
    public void DefaultVersion_IsV5_5_2()
    {
        var deployer = new PluginDeployer();

        Assert.Equal(XUnityVersion.V5_5_2, deployer.CurrentVersion);
    }

    [Fact]
    public void FindXUnityAssembly_WithNoPluginsDirectory_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.FindXUnityAssembly(tempDir, out var foundPath, out var errorDetail);

            Assert.False(result);
            Assert.Null(foundPath);
            Assert.NotNull(errorDetail);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindXUnityAssembly_WithEmptyPlugins_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.FindXUnityAssembly(tempDir, out var foundPath, out var errorDetail);

            Assert.False(result);
            Assert.Null(foundPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindXUnityAssembly_WithXUnityDll_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempDir, "BepInEx", "plugins"));
        File.WriteAllText(
            Path.Combine(tempDir, "BepInEx", "plugins", "XUnity.AutoTranslator.dll"),
            "dummy");

        try
        {
            var deployer = new PluginDeployer();
            var result = deployer.FindXUnityAssembly(tempDir, out var foundPath, out var errorDetail);

            Assert.True(result);
            Assert.NotNull(foundPath);
            Assert.Contains("XUnity.AutoTranslator", foundPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
