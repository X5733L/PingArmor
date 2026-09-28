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

    /// <summary>False when the IPv4 metric could not be read from the system (value -1).</summary>
    public bool HasMetricData => CurrentIPv4Metric >= 0;

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

    /// <summary>Non-fatal problems detected while evaluating the system (e.g. metric read failures).</summary>
    public List<string> Warnings { get; set; } = new();
}

public class OptimizationResult
{
    public bool Success { get; set; }
    public int ActionsApplied { get; set; }

    /// <summary>Actions skipped because the adapter is in back-off (third-party keeps reverting it).</summary>
    public int ActionsDeferred { get; set; }

    /// <summary>Actions that failed to apply or failed post-apply verification.</summary>
    public int ActionsFailed { get; set; }

    public List<string> Logs { get; set; } = new();
    public string? Error { get; set; }
}
