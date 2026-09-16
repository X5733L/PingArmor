using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PingArmor.Models;

namespace PingArmor.Services;

/// <summary>
/// Manages backup and restoration of system network settings modified by PingArmor.
/// Backup is stored as a JSON file next to the application config.
/// </summary>
public static class BackupService
{
    private static readonly object _lock = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Guard flag indicating settings have already been restored on application shutdown.
    /// </summary>
    public static bool HasRestoredOnExit { get; set; }

    /// <summary>
    /// Returns the default directory for backups.
    /// </summary>
    public static string GetDefaultBackupDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "backups");
    }

    /// <summary>
    /// Returns the default path for the backup file (in backups/backup.json).
    /// </summary>
    public static string GetDefaultBackupPath()
    {
        return Path.Combine(GetDefaultBackupDirectory(), "backup.json");
    }

    internal static string ResolveBackupPath(string? path)
    {
        if (path != null) return path;

        string defaultPath = GetDefaultBackupPath();
        if (!File.Exists(defaultPath))
        {
            string legacyPath = Path.Combine(AppContext.BaseDirectory, "backup.json");
            if (File.Exists(legacyPath))
            {
                try
                {
                    string dir = GetDefaultBackupDirectory();
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Move(legacyPath, defaultPath, overwrite: true);
                    return defaultPath;
                }
                catch
                {
                    return legacyPath;
                }
            }
        }
        return defaultPath;
    }

    /// <summary>
    /// Creates a backup of current system network settings if one does not already exist.
    /// Safe to call multiple times — will not overwrite an existing backup.
    /// </summary>
    /// <returns>True if a new backup was created, false if one already exists.</returns>
    public static bool CreateBackupIfNotExists(string? path = null)
    {
        path = ResolveBackupPath(path);

        lock (_lock)
        {
            if (File.Exists(path))
            {
                return false;
            }

            var snapshot = CaptureCurrentState();
            SaveSnapshot(snapshot, path);
            return true;
        }
    }

    /// <summary>
    /// Forces creation of a new backup, overwriting any existing one.
    /// </summary>
    public static void CreateBackup(string? path = null)
    {
        path = ResolveBackupPath(path);

        lock (_lock)
        {
            var snapshot = CaptureCurrentState();
            SaveSnapshot(snapshot, path);
        }
    }

    /// <summary>
    /// Loads an existing backup snapshot from disk.
    /// </summary>
    /// <returns>The backup snapshot, or null if no backup file exists.</returns>
    public static NetworkBackupSnapshot? LoadBackup(string? path = null)
    {
        path = ResolveBackupPath(path);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<NetworkBackupSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether a backup file exists.
    /// </summary>
    public static bool BackupExists(string? path = null)
    {
        path = ResolveBackupPath(path);
        return File.Exists(path);
    }

    /// <summary>
    /// Gets summary info about the existing backup file.
    /// </summary>
    public static BackupInfo GetBackupInfo(string? path = null)
    {
        path = ResolveBackupPath(path);
        if (!File.Exists(path))
        {
            return new BackupInfo(false, null, 0, null, path);
        }

        var snapshot = LoadBackup(path);
        if (snapshot == null)
        {
            return new BackupInfo(true, null, 0, null, path);
        }

        return new BackupInfo(true, snapshot.CreatedAt, snapshot.Adapters?.Count ?? 0, snapshot.MachineName, path);
    }

    /// <summary>
    /// Restores system network settings from a previously saved backup.
    /// </summary>
    /// <param name="path">Optional backup file path.</param>
    /// <param name="deleteBackupAfterRestore">If true, deletes the backup file upon successful restoration.</param>
    /// <returns>List of log messages describing what was restored.</returns>
    public static List<string> RestoreFromBackup(string? path = null, bool deleteBackupAfterRestore = false)
    {
        path = ResolveBackupPath(path);
        var logs = new List<string>();

        var snapshot = LoadBackup(path);
        if (snapshot == null)
        {
            logs.Add("[-] No backup file found. Nothing to restore.");
            return logs;
        }

        logs.Add($"[*] Restoring settings from backup created at {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}...");

        var psCommands = new List<string>();

        // 1. Restore adapter metrics
        foreach (var adapter in snapshot.Adapters)
        {
            try
            {
                if (adapter.AutomaticMetric)
                {
                    psCommands.Add($"Set-NetIPInterface -InterfaceIndex {adapter.InterfaceIndex} -AutomaticMetric Enabled -ErrorAction SilentlyContinue");
                    logs.Add($"[+] Adapter '{adapter.Name}' (id: {adapter.InterfaceIndex}): restored AutomaticMetric=Enabled");
                }
                else
                {
                    RunProcess("netsh.exe", $"int ipv4 set interface {adapter.InterfaceIndex} metric={adapter.IPv4Metric}");
                    RunProcess("netsh.exe", $"int ipv6 set interface {adapter.InterfaceIndex} metric={adapter.IPv4Metric}");
                    psCommands.Add($"Set-NetIPInterface -InterfaceIndex {adapter.InterfaceIndex} -AutomaticMetric Disabled -InterfaceMetric {adapter.IPv4Metric} -ErrorAction SilentlyContinue");
                    logs.Add($"[+] Adapter '{adapter.Name}' (id: {adapter.InterfaceIndex}): restored metric={adapter.IPv4Metric}");
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[-] Failed to restore adapter '{adapter.Name}': {ex.Message}");
            }
        }

        // 2. Restore IPv6 bindings
        foreach (var binding in snapshot.Ipv6Bindings)
        {
            try
            {
                if (binding.Ipv6Enabled)
                {
                    psCommands.Add($"Enable-NetAdapterBinding -Name '{binding.AdapterName}' -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue");
                    logs.Add($"[+] Adapter '{binding.AdapterName}': IPv6 re-enabled");
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[-] Failed to restore IPv6 on '{binding.AdapterName}': {ex.Message}");
            }
        }

        // Execute all PowerShell restore operations in a single batched process
        if (psCommands.Count > 0)
        {
            try
            {
                string batchedScript = string.Join("; ", psCommands);
                RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{batchedScript}\"");
            }
            catch (Exception ex)
            {
                logs.Add($"[-] Failed to execute batched PowerShell restore: {ex.Message}");
            }
        }

        // 3. Restore registry keys
        try
        {
            RestoreRegistryValue(
                Registry.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
                "DisableSmartNameResolution",
                snapshot.Registry.DisableSmartNameResolution);
            logs.Add(snapshot.Registry.DisableSmartNameResolution.HasValue
                ? $"[+] Registry DisableSmartNameResolution restored to {snapshot.Registry.DisableSmartNameResolution.Value}"
                : "[+] Registry DisableSmartNameResolution removed (was not set originally)");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Failed to restore DisableSmartNameResolution: {ex.Message}");
        }

        try
        {
            RestoreRegistryValue(
                Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
                "AutoDetect",
                snapshot.Registry.WpadAutoDetect);
            logs.Add(snapshot.Registry.WpadAutoDetect.HasValue
                ? $"[+] Registry WPAD AutoDetect restored to {snapshot.Registry.WpadAutoDetect.Value}"
                : "[+] Registry WPAD AutoDetect removed (was not set originally)");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Failed to restore WPAD AutoDetect: {ex.Message}");
        }

        logs.Add("[+] System settings restoration complete.");

        if (deleteBackupAfterRestore)
        {
            if (DeleteBackup(path))
            {
                logs.Add("[+] Backup file deleted after successful restoration (backups/backup.json).");
            }
        }

        return logs;
    }

    /// <summary>
    /// Deletes the backup file after successful restoration.
    /// </summary>
    public static bool DeleteBackup(string? path = null)
    {
        path = ResolveBackupPath(path);
        bool deleted = false;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                deleted = true;
            }
            string legacyPath = Path.Combine(AppContext.BaseDirectory, "backup.json");
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
                deleted = true;
            }
        }
        catch { }
        return deleted;
    }

    /// <summary>
    /// Performs a full reset of the Windows network stack (Winsock, TCP/IP, AutomaticMetric, IPv6, DNS cache).
    /// </summary>
    public static List<string> ResetWindowsNetworkStack()
    {
        var logs = new List<string>
        {
            "[*] Executing Windows network stack reset (Factory Defaults)..."
        };

        try
        {
            RunProcess("netsh.exe", "winsock reset");
            logs.Add("[+] Winsock catalog reset completed (netsh winsock reset)");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Winsock reset error: {ex.Message}");
        }

        try
        {
            RunProcess("netsh.exe", "int ip reset");
            logs.Add("[+] TCP/IP stack reset completed (netsh int ip reset)");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] TCP/IP reset error: {ex.Message}");
        }

        try
        {
            // Restore AutomaticMetric on all interfaces
            RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetIPInterface | Set-NetIPInterface -AutomaticMetric Enabled -ErrorAction SilentlyContinue\"");
            logs.Add("[+] AutomaticMetric restored to Enabled on all network interfaces");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Failed to restore AutomaticMetric: {ex.Message}");
        }

        try
        {
            // Re-enable IPv6 on all network adapters
            RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Enable-NetAdapterBinding -Name * -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\"");
            logs.Add("[+] IPv6 re-enabled on all network adapters");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Failed to re-enable IPv6: {ex.Message}");
        }

        try
        {
            // Restore DNS registry policies to Windows default
            using var dnsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", true);
            dnsKey?.DeleteValue("DisableSmartNameResolution", throwOnMissingValue: false);

            using var wpadKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            wpadKey?.SetValue("AutoDetect", 1, RegistryValueKind.DWord);

            logs.Add("[+] Registry DNS/WPAD policies reset to system defaults");
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Failed to reset registry policies: {ex.Message}");
        }

        try
        {
            DnsHelper.FlushDnsCache();
            logs.Add("[+] DNS resolver cache flushed");
        }
        catch { }

        logs.Add("[+] Windows network stack reset complete. A system restart is recommended.");
        return logs;
    }

    /// <summary>
    /// Opens the native Windows Network Reset settings applet.
    /// On Windows 11 (Build >= 22000), ms-settings:network-reset is deprecated and redirects to Home,
    /// so ms-settings:network-advancedsettings is used to directly access the page containing Network Reset.
    /// </summary>
    public static bool OpenWindowsNetworkResetSettings()
    {
        string[] targetUris = Environment.OSVersion.Version.Build >= 22000
            ? new[] { "ms-settings:network-advancedsettings", "ms-settings:network-reset", "ms-settings:network" }
            : new[] { "ms-settings:network-reset", "ms-settings:network-advancedsettings", "ms-settings:network" };

        foreach (var uri in targetUris)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                // Try next URI candidate
            }
        }
        return false;
    }

    // ---- Internal capture methods ----

    public static NetworkBackupSnapshot CaptureCurrentState()
    {
        var snapshot = new NetworkBackupSnapshot
        {
            CreatedAt = DateTime.Now,
            MachineName = Environment.MachineName
        };

        // 1. Capture adapter metrics via netsh
        snapshot.Adapters = CaptureAdapterMetrics();

        // 2. Capture IPv6 binding state on Wi-Fi adapters
        snapshot.Ipv6Bindings = CaptureIpv6Bindings();

        // 3. Capture registry keys
        snapshot.Registry = CaptureRegistryState();

        return snapshot;
    }

    internal static List<AdapterBackup> CaptureAdapterMetrics()
    {
        var result = new List<AdapterBackup>();

        // 1. Fast in-process WMI query
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT InterfaceIndex, InterfaceAlias, InterfaceMetric, AutomaticMetric FROM MSFT_NetIPInterface WHERE AddressFamily = 2"
                );
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    if (obj["InterfaceIndex"] != null &&
                        int.TryParse(obj["InterfaceIndex"].ToString(), out int ifIndex) &&
                        obj["InterfaceMetric"] != null &&
                        int.TryParse(obj["InterfaceMetric"].ToString(), out int metric))
                    {
                        string name = obj["InterfaceAlias"]?.ToString() ?? $"Interface {ifIndex}";
                        bool autoMetric = false;
                        if (obj["AutomaticMetric"] != null)
                        {
                            if (int.TryParse(obj["AutomaticMetric"].ToString(), out int autoVal))
                            {
                                autoMetric = (autoVal == 1);
                            }
                            else if (bool.TryParse(obj["AutomaticMetric"].ToString(), out bool bVal))
                            {
                                autoMetric = bVal;
                            }
                        }

                        result.Add(new AdapterBackup
                        {
                            InterfaceIndex = ifIndex,
                            Name = name,
                            IPv4Metric = metric,
                            AutomaticMetric = autoMetric
                        });
                    }
                }

                if (result.Count > 0)
                {
                    return result;
                }
            }
            catch
            {
                // Fall back to PowerShell query below if WMI query fails
            }
        }

        // 2. Fallback to PowerShell if WMI is unavailable
        try
        {
            // Use PowerShell to get metrics and AutomaticMetric flag reliably
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetIPInterface -AddressFamily IPv4 | Select-Object -Property InterfaceIndex, InterfaceAlias, InterfaceMetric, AutomaticMetric | ForEach-Object { \\\"$($_.InterfaceIndex)|$($_.InterfaceAlias)|$($_.InterfaceMetric)|$($_.AutomaticMetric)\\\" }\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return result;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Trim().Split('|');
                if (parts.Length >= 4 && int.TryParse(parts[0], out int ifIndex) && int.TryParse(parts[2], out int metric))
                {
                    bool autoMetric = parts[3].Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                    result.Add(new AdapterBackup
                    {
                        InterfaceIndex = ifIndex,
                        Name = parts[1].Trim(),
                        IPv4Metric = metric,
                        AutomaticMetric = autoMetric
                    });
                }
            }
        }
        catch { }

        return result;
    }

    internal static List<Ipv6BindingBackup> CaptureIpv6Bindings()
    {
        var result = new List<Ipv6BindingBackup>();

        // 1. Fast in-process WMI query
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var wifiAdapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .Select(n => n.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (wifiAdapters.Count == 0)
                {
                    return result; // No Wi-Fi interfaces to capture
                }

                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT Name, Enabled FROM MSFT_NetAdapterBindingSettingData WHERE ComponentID = 'ms_tcpip6'"
                );
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    string? name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && wifiAdapters.Contains(name))
                    {
                        bool enabled = obj["Enabled"] != null &&
                                       Convert.ToBoolean(obj["Enabled"]);
                        result.Add(new Ipv6BindingBackup
                        {
                            AdapterName = name,
                            Ipv6Enabled = enabled
                        });
                    }
                }

                if (result.Count > 0)
                {
                    return result;
                }
            }
            catch
            {
                // Fall back to PowerShell query below if WMI query fails
            }
        }

        // 2. Fallback to PowerShell if WMI is unavailable
        try
        {
            // Check IPv6 binding on Wi-Fi adapters
            var wifiAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Select(n => n.Name)
                .ToList();

            if (wifiAdapters.Count == 0)
            {
                return result;
            }

            foreach (var adapterName in wifiAdapters)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-NetAdapterBinding -Name '{adapterName}' -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue).Enabled\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) continue;

                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(3000);

                    bool ipv6Enabled = output.Equals("True", StringComparison.OrdinalIgnoreCase);
                    result.Add(new Ipv6BindingBackup
                    {
                        AdapterName = adapterName,
                        Ipv6Enabled = ipv6Enabled
                    });
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    internal static RegistryBackup CaptureRegistryState()
    {
        var backup = new RegistryBackup();

        // DisableSmartNameResolution
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient");
            if (key != null)
            {
                var value = key.GetValue("DisableSmartNameResolution");
                if (value is int intVal)
                {
                    backup.DisableSmartNameResolution = intVal;
                }
            }
            // If key/value doesn't exist, null indicates "was not set"
        }
        catch { }

        // WPAD AutoDetect
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key != null)
            {
                var value = key.GetValue("AutoDetect");
                if (value is int intVal)
                {
                    backup.WpadAutoDetect = intVal;
                }
            }
        }
        catch { }

        return backup;
    }

    // ---- Helpers ----

    private static void SaveSnapshot(NetworkBackupSnapshot snapshot, string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(path, json);
    }

    private static void RestoreRegistryValue(RegistryKey rootKey, string subKeyPath, string valueName, int? originalValue)
    {
        if (originalValue.HasValue)
        {
            using var key = rootKey.CreateSubKey(subKeyPath, true);
            key?.SetValue(valueName, originalValue.Value, RegistryValueKind.DWord);
        }
        else
        {
            // Value did not exist originally — remove it
            try
            {
                using var key = rootKey.OpenSubKey(subKeyPath, true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
            catch { }
        }
    }

    private static void RunProcess(string fileName, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch { }
    }
}
