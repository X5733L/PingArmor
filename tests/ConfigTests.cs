using System;
using System.Collections.Generic;
using System.IO;
using PingArmor.Config;
using Xunit;

namespace PingArmor.Tests;

public class ConfigTests : IDisposable
{
    private readonly string _tempConfigPath;

    public ConfigTests()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultConfig()
    {
        var config = AppConfig.Load(_tempConfigPath);

        Assert.NotNull(config);
        Assert.Equal(5, config.PrimaryEthernetMetric);
        Assert.Equal(10, config.PrimaryWifiMetric);
        Assert.Equal(500, config.VirtualAdapterMetric);
        Assert.Equal(100, config.DisconnectedAdapterMetric);
        Assert.True(config.DisableSmartNameResolution);
        Assert.True(config.FlushDnsOnChange);
        Assert.True(config.EnableWlanOptimizer);
        Assert.Equal("ru", config.Language);
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistConfigValues()
    {
        var config = new AppConfig
        {
            PrimaryEthernetMetric = 1,
            PrimaryWifiMetric = 2,
            VirtualAdapterMetric = 800,
            CheckIntervalSeconds = 15,
            Language = "en",
            ExcludeAdapters = new List<string> { "Tailscale", "Hyper-V" }
        };

        config.Save(_tempConfigPath);

        var loaded = AppConfig.Load(_tempConfigPath);

        Assert.Equal(1, loaded.PrimaryEthernetMetric);
        Assert.Equal(2, loaded.PrimaryWifiMetric);
        Assert.Equal(800, loaded.VirtualAdapterMetric);
        Assert.Equal(15, loaded.CheckIntervalSeconds);
        Assert.Equal("en", loaded.Language);
        Assert.Contains("Tailscale", loaded.ExcludeAdapters);
        Assert.Contains("Hyper-V", loaded.ExcludeAdapters);
    }

    [Fact]
    public void DefaultConfigPath_ShouldBeInConfigsDirectory()
    {
        string dir = AppConfig.GetDefaultConfigDirectory();
        string path = AppConfig.GetDefaultConfigPath();

        Assert.EndsWith("configs", dir);
        Assert.Equal(Path.Combine(dir, "config.json"), path);
    }

    [Fact]
    public void Save_WhenDirectoryDoesNotExist_CreatesDirectoryAndSaves()
    {
        string subDir = Path.Combine(Path.GetTempPath(), $"sub_{Guid.NewGuid()}");
        string customPath = Path.Combine(subDir, "config.json");

        try
        {
            var config = new AppConfig { CheckIntervalSeconds = 42 };
            config.Save(customPath);

            Assert.True(File.Exists(customPath));
            var loaded = AppConfig.Load(customPath);
            Assert.Equal(42, loaded.CheckIntervalSeconds);
        }
        finally
        {
            if (Directory.Exists(subDir))
            {
                Directory.Delete(subDir, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempConfigPath))
            {
                File.Delete(_tempConfigPath);
            }
        }
        catch { }
    }
}
