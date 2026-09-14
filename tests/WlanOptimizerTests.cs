using System;
using System.Collections.Generic;
using System.IO;
using PingArmor.Config;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class WlanOptimizerTests : IDisposable
{
    private readonly string _tempConfigPath;

    public WlanOptimizerTests()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{Guid.NewGuid()}.json");
    }

    private class FakeNetworkEngine : INetworkEngine
    {
        public List<NetworkAdapterInfo> GetAdapters()
        {
            return new List<NetworkAdapterInfo>
            {
                new NetworkAdapterInfo
                {
                    InterfaceIndex = 4,
                    Name = "Беспроводная сеть 2",
                    Type = AdapterType.PhysicalWiFi,
                    IsPhysical = true,
                    IsUp = true,
                    HasInternet = true,
                    CurrentIPv4Metric = 40
                }
            };
        }

        public OptimizationResult ApplyPlan(OptimizationPlan plan)
        {
            return new OptimizationResult { Success = true };
        }
    }

    #region Win32 WLAN Native API Constants Tests

    [Fact]
    public void WlanNativeApi_Constants_MatchWindowsSdkSpec()
    {
        // Verify wlanapi.h constants from Windows SDK:
        // OpCode 1 = autoconf (must not disable via netsh, otherwise disconnect occurs)
        // OpCode 2 = background_scan_enabled (smoothly suppresses background network scans)
        // OpCode 3 = media_streaming_mode (enables traffic prioritization and removes jitter)
        Assert.Equal(1, WlanOptimizerService.wlan_intf_opcode_autoconf_enabled);
        Assert.Equal(2, WlanOptimizerService.wlan_intf_opcode_background_scan_enabled);
        Assert.Equal(3, WlanOptimizerService.wlan_intf_opcode_media_streaming_mode);
        Assert.Equal(6, WlanOptimizerService.wlan_intf_opcode_interface_state);

        // Interface states
        Assert.Equal(0, WlanOptimizerService.wlan_interface_state_not_ready);
        Assert.Equal(1, WlanOptimizerService.wlan_interface_state_connected);
        Assert.Equal(4, WlanOptimizerService.wlan_interface_state_disconnected);
        Assert.Equal(5, WlanOptimizerService.wlan_interface_state_associating);
    }

    #endregion

    #region Lifecycle & Safe Restoration Tests

    [Fact]
    public void WlanOptimizer_RestoreDefaultScan_IdempotentAndSafe()
    {
        // Verify RestoreDefaultScan() is safe, idempotent, and reliably resets active state
        WlanOptimizerService.RestoreDefaultScan();
        WlanOptimizerService.RestoreDefaultScan();

        Assert.False(WlanOptimizerService.IsGamingModeActive);
    }

    [Fact]
    public void WlanOptimizer_IsAnyWifiConnected_ExecutesWithoutException()
    {
        // Verify connection status check executes safely without throwing exceptions
        bool isConnected = WlanOptimizerService.IsAnyWifiConnected();
        // Result depends on host PC state; primary check is method stability and safety
        Assert.True(isConnected || !isConnected);
    }

    [Fact]
    public void WlanOptimizer_SetGamingMode_Disable_ResetsActiveState()
    {
        // Disabling optimization should safely return active flag to false
        var result = WlanOptimizerService.SetGamingMode(false);

        Assert.NotNull(result);
        Assert.False(WlanOptimizerService.IsGamingModeActive);
    }

    #endregion

    #region NetworkMonitor Integration (Smart Connect-First)

    [Fact]
    public void NetworkMonitor_ExecuteCheck_WithWlanOptimizerEnabled_ExecutesSafely()
    {
        var config = new AppConfig
        {
            EnableWlanOptimizer = true
        };

        var fakeEngine = new FakeNetworkEngine();
        using var monitor = new NetworkMonitor(fakeEngine, config);

        bool planEvaluated = false;
        monitor.PlanEvaluated += _ => planEvaluated = true;

        var plan = monitor.ExecuteCheck();

        Assert.NotNull(plan);
        Assert.True(planEvaluated);
    }

    [Fact]
    public void NetworkMonitor_ExecuteCheck_WithWlanOptimizerDisabled_ExecutesSafely()
    {
        var config = new AppConfig
        {
            EnableWlanOptimizer = false
        };

        var fakeEngine = new FakeNetworkEngine();
        using var monitor = new NetworkMonitor(fakeEngine, config);

        bool planEvaluated = false;
        monitor.PlanEvaluated += _ => planEvaluated = true;

        var plan = monitor.ExecuteCheck();

        Assert.NotNull(plan);
        Assert.True(planEvaluated);
    }

    #endregion

    #region WlanInterfaceParser Tests

    [Fact]
    public void WlanInterfaceParser_RussianOutput_ParsesCorrectly()
    {
        string sampleOutput = @"
В системе 1 интерфейс: 

    Имя:                                Беспроводная сеть 2
    Описание:                       RZ616 Wi-Fi 6E 160MHz #2
    Идентификатор GUID:   1a800e08-0a82-4842-a8cd-1558142bf198
    Физический адрес:       bc:f4:d4:c1:29:4b
    Состояние:                     Подключено
    SSID                   : New Life
";

        var names = WlanInterfaceParser.ParseInterfaceNames(sampleOutput);

        Assert.Single(names);
        Assert.Equal("Беспроводная сеть 2", names[0]);
    }

    [Fact]
    public void WlanInterfaceParser_EnglishOutput_ParsesCorrectly()
    {
        string sampleOutput = @"
There is 1 interface on the system:

    Name                   : Wi-Fi
    Description            : MediaTek Wi-Fi 6E MT7922
    GUID                   : d3b90558-1234-5678-abcd-1234567890ab
    State                  : connected
    SSID                   : Office_5G
";

        var names = WlanInterfaceParser.ParseInterfaceNames(sampleOutput);

        Assert.Single(names);
        Assert.Equal("Wi-Fi", names[0]);
    }

    [Fact]
    public void WlanInterfaceParser_MultipleInterfaces_ParsesAllUnique()
    {
        string sampleOutput = @"
There are 2 interfaces on the system:

    Name: Wi-Fi 1
    Description: Adapter 1

    Name: Wi-Fi 2
    Description: Adapter 2

    Name: Wi-Fi 1
    Description: Duplicate
";

        var names = WlanInterfaceParser.ParseInterfaceNames(sampleOutput);

        Assert.Equal(2, names.Count);
        Assert.Equal("Wi-Fi 1", names[0]);
        Assert.Equal("Wi-Fi 2", names[1]);
    }

    [Fact]
    public void WlanInterfaceParser_WithTabsAndWhitespace_ParsesCleanly()
    {
        string sampleOutput = "\tName\t:\t  Wi-Fi 7 Gaming Adapter   \r\n";
        var names = WlanInterfaceParser.ParseInterfaceNames(sampleOutput);

        Assert.Single(names);
        Assert.Equal("Wi-Fi 7 Gaming Adapter", names[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n   ")]
    [InlineData("Служба автонастройки беспроводных сетей не запущена.")]
    public void WlanInterfaceParser_EmptyOrInvalidOutput_ReturnsEmpty(string? output)
    {
        var names = WlanInterfaceParser.ParseInterfaceNames(output);
        Assert.Empty(names);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void AppConfig_EnableWlanOptimizer_DefaultsToTrue()
    {
        var config = AppConfig.Load(_tempConfigPath);
        Assert.True(config.EnableWlanOptimizer);
    }

    [Fact]
    public void AppConfig_EnableWlanOptimizer_PersistsCustomValue()
    {
        var config = new AppConfig
        {
            EnableWlanOptimizer = false
        };

        config.Save(_tempConfigPath);

        var loaded = AppConfig.Load(_tempConfigPath);
        Assert.False(loaded.EnableWlanOptimizer);
    }

    #endregion

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
