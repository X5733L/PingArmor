using System;
using System.Collections.Generic;

namespace PingArmor.Models;

/// <summary>
/// Snapshot of system network settings captured before PingArmor applies optimizations.
/// Used to safely restore original configuration.
/// </summary>
public class NetworkBackupSnapshot
{
    public DateTime CreatedAt { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public List<AdapterBackup> Adapters { get; set; } = new();
    public RegistryBackup Registry { get; set; } = new();
    public List<Ipv6BindingBackup> Ipv6Bindings { get; set; } = new();
}

/// <summary>
/// Backup of a single network adapter's metric settings.
/// </summary>
public class AdapterBackup
{
    public int InterfaceIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int IPv4Metric { get; set; }
    public bool AutomaticMetric { get; set; }
}

/// <summary>
/// Backup of registry keys modified by PingArmor (DNS policies, WPAD).
/// </summary>
public class RegistryBackup
{
    /// <summary>
    /// Original value of HKLM\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient\DisableSmartNameResolution.
    /// Null means the key did not exist.
    /// </summary>
    public int? DisableSmartNameResolution { get; set; }

    /// <summary>
    /// Original value of HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings\AutoDetect.
    /// Null means the key did not exist.
    /// </summary>
    public int? WpadAutoDetect { get; set; }
}

/// <summary>
/// Backup of IPv6 binding state on a specific adapter.
/// </summary>
public class Ipv6BindingBackup
{
    public string AdapterName { get; set; } = string.Empty;
    public bool Ipv6Enabled { get; set; }
}
