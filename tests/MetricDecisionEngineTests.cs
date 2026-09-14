using System.Collections.Generic;
using System.Linq;
using PingArmor.Config;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class MetricDecisionEngineTests
{
    private readonly AppConfig _config = new()
    {
        PrimaryEthernetMetric = 5,
        PrimaryWifiMetric = 10,
        VirtualAdapterMetric = 500,
        DisconnectedAdapterMetric = 100,
        DisableSmartNameResolution = true,
        DisableWpad = true,
        FlushDnsOnChange = true
    };

    [Fact]
    public void Evaluate_WhenVpnOverridesMetric_TriggersOptimization()
    {
        // Wi-Fi (10), but VPN hijacked its metric to 1!
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Беспроводная сеть 2",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 10
        };

        var vpn = new NetworkAdapterInfo
        {
            InterfaceIndex = 25,
            Name = "OpenVPN Connect DCO Adapter",
            Type = AdapterType.VirtualOrVpn,
            IsPhysical = false,
            IsUp = true,
            CurrentIPv4Metric = 1 // Priority hijacked!
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, vpn };
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Assert.True(plan.NeedsOptimization);
        Assert.Equal(4, plan.PrimaryAdapter?.InterfaceIndex);

        var vpnAction = plan.Actions.FirstOrDefault(a => a.InterfaceIndex == 25);
        Assert.NotNull(vpnAction);
        Assert.Equal(500, vpnAction.TargetMetric);
        Assert.Equal(1, vpnAction.CurrentMetric);
    }

    [Fact]
    public void Evaluate_WhenMetricsAlreadyOptimized_DoesNotTriggerOptimization()
    {
        // Rest state: Wi-Fi 10, VPN 500
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Беспроводная сеть 2",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 10,
            AutomaticMetric = false
        };

        var vpn = new NetworkAdapterInfo
        {
            InterfaceIndex = 25,
            Name = "OpenVPN Connect DCO Adapter",
            Type = AdapterType.VirtualOrVpn,
            IsPhysical = false,
            IsUp = true,
            CurrentIPv4Metric = 500,
            AutomaticMetric = false
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, vpn };
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Assert.False(plan.NeedsOptimization);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Evaluate_WhenDisconnectedPhysicalAdapter_SetsMetric100()
    {
        var activeWifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Wi-Fi",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 10
        };

        var disconnectedEthernet = new NetworkAdapterInfo
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Type = AdapterType.PhysicalEthernet,
            IsPhysical = true,
            IsUp = false,
            CurrentIPv4Metric = 25 // Not 100
        };

        var adapters = new List<NetworkAdapterInfo> { activeWifi, disconnectedEthernet };
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Assert.True(plan.NeedsOptimization);
        var ethAction = plan.Actions.FirstOrDefault(a => a.InterfaceIndex == 7);
        Assert.NotNull(ethAction);
        Assert.Equal(100, ethAction.TargetMetric);
    }

    [Fact]
    public void Evaluate_WhenAdapterExcluded_SkipsOptimizationForIt()
    {
        var configWithExclude = new AppConfig
        {
            PrimaryWifiMetric = 10,
            VirtualAdapterMetric = 500,
            ExcludeAdapters = new List<string> { "SpecialVpn" }
        };

        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Wi-Fi",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 10
        };

        var excludedVpn = new NetworkAdapterInfo
        {
            InterfaceIndex = 99,
            Name = "SpecialVpn",
            Type = AdapterType.VirtualOrVpn,
            IsPhysical = false,
            IsUp = true,
            CurrentIPv4Metric = 1 // Low metric, but adapter is excluded/whitelisted
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, excludedVpn };
        var plan = MetricDecisionEngine.Evaluate(adapters, configWithExclude);

        Assert.False(plan.NeedsOptimization);
        Assert.Empty(plan.Actions);
    }
}
