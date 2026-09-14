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

    [Fact]
    public void Evaluate_SecondaryPhysicalAdapterWithLowMetric_GetsSecondaryMetric()
    {
        // Wi-Fi is primary with internet, Ethernet is secondary connected without internet
        // Ethernet has a low metric that could compete with primary
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Wi-Fi",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 10,
            AutomaticMetric = false
        };

        var secondaryEthernet = new NetworkAdapterInfo
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Type = AdapterType.PhysicalEthernet,
            IsPhysical = true,
            IsUp = true,
            HasInternet = false,
            CurrentIPv4Metric = 5, // Lower than primary Wi-Fi metric (10) — routing conflict!
            AutomaticMetric = false
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, secondaryEthernet };
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Assert.True(plan.NeedsOptimization);
        Assert.Equal(4, plan.PrimaryAdapter?.InterfaceIndex);

        var ethAction = plan.Actions.FirstOrDefault(a => a.InterfaceIndex == 7);
        Assert.NotNull(ethAction);
        Assert.Equal(50, ethAction.TargetMetric); // SecondaryPhysicalMetric default
        Assert.Contains("Secondary physical adapter", ethAction.Reason);
    }

    [Fact]
    public void Evaluate_BugRepro_EthernetNoInternetWithWifiPrimary_FixesRoutingConflict()
    {
        // Exact reproduction of the tester's scenario:
        // Ethernet connected to LAN (no internet, metric=25)
        // Wi-Fi connected to router (internet, metric=40)
        // PingArmor should set Wi-Fi as primary (metric=10) and
        // raise Ethernet metric to prevent routing conflict
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Беспроводная сеть 2",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 40,
            AutomaticMetric = true
        };

        var ethernet = new NetworkAdapterInfo
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Type = AdapterType.PhysicalEthernet,
            IsPhysical = true,
            IsUp = true,
            HasInternet = false,
            CurrentIPv4Metric = 25,
            AutomaticMetric = true
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, ethernet };
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Assert.True(plan.NeedsOptimization);
        // Wi-Fi should be selected as primary (has internet)
        Assert.Equal(4, plan.PrimaryAdapter?.InterfaceIndex);

        // Wi-Fi should get PrimaryWifiMetric (10)
        var wifiAction = plan.Actions.FirstOrDefault(a => a.InterfaceIndex == 4);
        Assert.NotNull(wifiAction);
        Assert.Equal(10, wifiAction.TargetMetric);

        // Ethernet should get SecondaryPhysicalMetric (50)
        // Previously this adapter was SKIPPED, causing routing conflict and ping spikes
        var ethAction = plan.Actions.FirstOrDefault(a => a.InterfaceIndex == 7);
        Assert.NotNull(ethAction);
        Assert.Equal(50, ethAction.TargetMetric);
    }
}

