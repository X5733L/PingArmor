using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PingArmor.Models;

namespace PingArmor.Services;

public class AdapterClassifier
{
    private static readonly string[] VirtualKeywords = new[]
    {
        "virtual", "vpn", "tap", "wintun", "fortinet", "openvpn", "wireguard",
        "tailscale", "zerotier", "cisco", "anyconnect", "cloudflare", "warp",
        "neko", "v2ray", "sing-box", "hyper-v", "vethernet", "loopback",
        "bluetooth", "wan miniport", "pseudo-interface"
    };

    private static readonly string[] EthernetKeywords = new[]
    {
        "ethernet", "realtek", "intel", "killer", "broadcom", "aqc", "pcie",
        "gigabit", "2.5gbe", "10gbe", "lan controller", "i225", "i226"
    };

    private static readonly string[] WifiKeywords = new[]
    {
        "wi-fi", "wifi", "wireless", "802.11", "rz616", "ax200", "ax201",
        "ax210", "ax211", "be200", "mediatek", "qualcomm", "atheros"
    };

    public static AdapterType Classify(string description, string name, string mediaType, bool isPhysical)
    {
        string descLower = (description ?? string.Empty).ToLowerInvariant();
        string nameLower = (name ?? string.Empty).ToLowerInvariant();
        string mediaLower = (mediaType ?? string.Empty).ToLowerInvariant();

        // Check for virtual / VPN indicators first
        foreach (var keyword in VirtualKeywords)
        {
            if (descLower.Contains(keyword) || nameLower.Contains(keyword))
            {
                return AdapterType.VirtualOrVpn;
            }
        }

        if (!isPhysical)
        {
            return AdapterType.VirtualOrVpn;
        }

        // Check for Wi-Fi
        if (mediaLower.Contains("802.11") || WifiKeywords.Any(k => descLower.Contains(k) || nameLower.Contains(k)))
        {
            return AdapterType.PhysicalWiFi;
        }

        // Check for Ethernet
        if (mediaLower.Contains("802.3") || EthernetKeywords.Any(k => descLower.Contains(k) || nameLower.Contains(k)))
        {
            return AdapterType.PhysicalEthernet;
        }

        return isPhysical ? AdapterType.PhysicalEthernet : AdapterType.Other;
    }

    public static NetworkAdapterInfo? DeterminePrimaryAdapter(IEnumerable<NetworkAdapterInfo> adapters)
    {
        var list = adapters.ToList();

        // 1. Physical Ethernet connected and confirmed internet
        var ethernetInternet = list.FirstOrDefault(a => a.Type == AdapterType.PhysicalEthernet && a.IsUp && a.HasInternet);
        if (ethernetInternet != null) return ethernetInternet;

        // 2. Physical Wi-Fi connected and confirmed internet
        var wifiInternet = list.FirstOrDefault(a => a.Type == AdapterType.PhysicalWiFi && a.IsUp && a.HasInternet);
        if (wifiInternet != null) return wifiInternet;

        // 3. Any active physical Ethernet (e.g. NCSI has not probed internet yet)
        var activeEthernet = list.FirstOrDefault(a => a.Type == AdapterType.PhysicalEthernet && a.IsUp);
        if (activeEthernet != null) return activeEthernet;

        // 4. Any active physical Wi-Fi
        var activeWifi = list.FirstOrDefault(a => a.Type == AdapterType.PhysicalWiFi && a.IsUp);
        if (activeWifi != null) return activeWifi;

        // 5. Fallback: Any active adapter with confirmed internet (that is not virtual if possible)
        var fallbackInternet = list.FirstOrDefault(a => a.IsUp && a.HasInternet && a.Type != AdapterType.VirtualOrVpn)
                              ?? list.FirstOrDefault(a => a.IsUp && a.HasInternet);

        return fallbackInternet;
    }
}
