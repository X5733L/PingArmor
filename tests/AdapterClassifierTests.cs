using System.Collections.Generic;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class AdapterClassifierTests
{
    [Theory]
    [InlineData("Realtek Gaming 2.5GbE Family Controller", "Ethernet", "Ethernet", true, AdapterType.PhysicalEthernet)]
    [InlineData("Intel(R) Ethernet Connection I219-V", "Ethernet 1", "802.3", true, AdapterType.PhysicalEthernet)]
    [InlineData("Killer E3100X 2.5 Gigabit Ethernet Controller", "LAN", "Ethernet", true, AdapterType.PhysicalEthernet)]
    public void Classify_ShouldIdentifyPhysicalEthernet(string desc, string name, string media, bool isPhys, AdapterType expected)
    {
        var result = AdapterClassifier.Classify(desc, name, media, isPhys);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("RZ616 Wi-Fi 6E 160MHz #2", "Беспроводная сеть 2", "Native 802.11", true, AdapterType.PhysicalWiFi)]
    [InlineData("Intel(R) Wi-Fi 6 AX200 160MHz", "Wi-Fi", "802.11", true, AdapterType.PhysicalWiFi)]
    [InlineData("Qualcomm FastConnect 6900 Wi-Fi 6E", "WLAN", "Wireless80211", true, AdapterType.PhysicalWiFi)]
    public void Classify_ShouldIdentifyPhysicalWiFi(string desc, string name, string media, bool isPhys, AdapterType expected)
    {
        var result = AdapterClassifier.Classify(desc, name, media, isPhys);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Fortinet Virtual Ethernet Adapter (NDIS 6.30)", "Ethernet 3", "Ethernet", false)]
    [InlineData("OpenVPN Data Channel Offload", "OpenVPN Connect DCO Adapter", "Ethernet", false)]
    [InlineData("TAP-Windows Adapter V9", "Ethernet 2", "Ethernet", false)]
    [InlineData("Hyper-V Virtual Ethernet Adapter", "vEthernet (Default Switch)", "Ethernet", false)]
    [InlineData("WireGuard Tunnel", "wg0", "Tunnel", false)]
    [InlineData("Wintun Userspace Tunnel", "sing-box-tun", "Tunnel", false)]
    public void Classify_ShouldIdentifyVirtualOrVpn(string desc, string name, string media, bool isPhys)
    {
        var result = AdapterClassifier.Classify(desc, name, media, isPhys);
        Assert.Equal(AdapterType.VirtualOrVpn, result);
    }

    [Fact]
    public void DeterminePrimaryAdapter_ShouldPreferEthernetWithInternet_OverWiFiWithInternet()
    {
        var ethernet = new NetworkAdapterInfo
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Type = AdapterType.PhysicalEthernet,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true
        };
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Wi-Fi",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true
        };

        var adapters = new List<NetworkAdapterInfo> { wifi, ethernet };
        var primary = AdapterClassifier.DeterminePrimaryAdapter(adapters);

        Assert.NotNull(primary);
        Assert.Equal(7, primary.InterfaceIndex);
        Assert.Equal("Ethernet", primary.Name);
    }

    [Fact]
    public void DeterminePrimaryAdapter_ShouldSelectWiFi_WhenEthernetIsDisconnected()
    {
        var disconnectedEthernet = new NetworkAdapterInfo
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Type = AdapterType.PhysicalEthernet,
            IsPhysical = true,
            IsUp = false,
            HasInternet = false
        };
        var activeWifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Беспроводная сеть 2",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true
        };

        var adapters = new List<NetworkAdapterInfo> { disconnectedEthernet, activeWifi };
        var primary = AdapterClassifier.DeterminePrimaryAdapter(adapters);

        Assert.NotNull(primary);
        Assert.Equal(4, primary.InterfaceIndex);
        Assert.Equal("Беспроводная сеть 2", primary.Name);
    }

    [Fact]
    public void DeterminePrimaryAdapter_ShouldIgnoreVpn_WhenPhysicalAdapterAvailable()
    {
        var vpn = new NetworkAdapterInfo
        {
            InterfaceIndex = 25,
            Name = "OpenVPN Connect DCO Adapter",
            Type = AdapterType.VirtualOrVpn,
            IsPhysical = false,
            IsUp = true,
            HasInternet = true
        };
        var wifi = new NetworkAdapterInfo
        {
            InterfaceIndex = 4,
            Name = "Wi-Fi",
            Type = AdapterType.PhysicalWiFi,
            IsPhysical = true,
            IsUp = true,
            HasInternet = true
        };

        var adapters = new List<NetworkAdapterInfo> { vpn, wifi };
        var primary = AdapterClassifier.DeterminePrimaryAdapter(adapters);

        Assert.NotNull(primary);
        Assert.Equal(4, primary.InterfaceIndex);
    }
}
