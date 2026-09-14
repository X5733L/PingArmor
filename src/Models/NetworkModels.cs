using System.Collections.Generic;

namespace PingArmor.Models;

public enum AdapterType
{
    PhysicalEthernet,
    PhysicalWiFi,
    VirtualOrVpn,
    Other
}

public class NetworkAdapterInfo
{
    public int InterfaceIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPhysical { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public bool IsUp { get; set; }
    public bool HasInternet { get; set; }
    public int CurrentIPv4Metric { get; set; } = -1;
    public int CurrentIPv6Metric { get; set; } = -1;
    public bool AutomaticMetric { get; set; }
    public AdapterType Type { get; set; } = AdapterType.Other;

    public override string ToString() =>
        $"[{InterfaceIndex}] {Name} ({Type}) - Up: {IsUp}, Internet: {HasInternet}, Metric: {CurrentIPv4Metric}";
}

public class OptimizationAction
{
    public int InterfaceIndex { get; set; }
    public string InterfaceAlias { get; set; } = string.Empty;
    public int TargetMetric { get; set; }
    public int CurrentMetric { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool DisableIPv6 { get; set; }

    public override string ToString() =>
        $"[ifIndex: {InterfaceIndex} '{InterfaceAlias}'] {CurrentMetric} -> {TargetMetric} ({Reason})";
}

public class OptimizationPlan
{
    public bool NeedsOptimization { get; set; }
    public NetworkAdapterInfo? PrimaryAdapter { get; set; }
    public List<OptimizationAction> Actions { get; set; } = new();
    public bool DisableSmartNameResolution { get; set; } = true;
    public bool DisableWpad { get; set; } = true;
    public bool FlushDns { get; set; } = true;
    public string Summary { get; set; } = string.Empty;
}

public class OptimizationResult
{
    public bool Success { get; set; }
    public int ActionsApplied { get; set; }
    public List<string> Logs { get; set; } = new();
    public string? Error { get; set; }
}
