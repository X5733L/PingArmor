using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
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
    /// Returns the default path for the backup file (next to config.json).
    /// </summary>
    public static string GetDefaultBackupPath()
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "backup.json");
    }

    /// <summary>
    /// Creates a backup of current system network settings if one does not already exist.
    /// Safe to call multiple times — will not overwrite an existing backup.
    /// </summary>
    /// <returns>True if a new backup was created, false if one already exists.</returns>
    public static bool CreateBackupIfNotExists(string? path = null)
    {
        path ??= GetDefaultBackupPath();

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
        path ??= GetDefaultBackupPath();

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
        path ??= GetDefaultBackupPath();

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
        path ??= GetDefaultBackupPath();
        return File.Exists(path);
    }

    /// <summary>
    /// Restores system network settings from a previously saved backup.
    /// </summary>
    /// <returns>List of log messages describing what was restored.</returns>
    public static List<string> RestoreFromBackup(string? path = null)
    {
        path ??= GetDefaultBackupPath();
        var logs = new List<string>();

        var snapshot = LoadBackup(path);
        if (snapshot == null)
        {
            logs.Add("[-] No backup file found. Nothing to restore.");
            return logs;
        }

        logs.Add($"[*] Restoring settings from backup created at {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}...");

        // 1. Restore adapter metrics
        foreach (var adapter in snapshot.Adapters)
        {
            try
            {
                if (adapter.AutomaticMetric)
                {
                    // Restore automatic metric assignment
                    RunProcess("powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetIPInterface -InterfaceIndex {adapter.InterfaceIndex} -AutomaticMetric Enabled -ErrorAction SilentlyContinue\"");
                    logs.Add($"[+] Adapter '{adapter.Name}' (id: {adapter.InterfaceIndex}): restored AutomaticMetric=Enabled");
                }
                else
                {
                    // Restore specific metric value
                    RunProcess("netsh.exe", $"int ipv4 set interface {adapter.InterfaceIndex} metric={adapter.IPv4Metric}");
                    RunProcess("netsh.exe", $"int ipv6 set interface {adapter.InterfaceIndex} metric={adapter.IPv4Metric}");
                    RunProcess("powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetIPInterface -InterfaceIndex {adapter.InterfaceIndex} -AutomaticMetric Disabled -InterfaceMetric {adapter.IPv4Metric} -ErrorAction SilentlyContinue\"");
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
                    RunProcess("powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -Command \"Enable-NetAdapterBinding -Name '{binding.AdapterName}' -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\"");
                    logs.Add($"[+] Adapter '{binding.AdapterName}': IPv6 re-enabled");
                }
            }
            catch (Exception ex)
            {
                logs.Add($"[-] Failed to restore IPv6 on '{binding.AdapterName}': {ex.Message}");
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
        return logs;
    }

    /// <summary>
    /// Deletes the backup file after successful restoration.
    /// </summary>
    public static bool DeleteBackup(string? path = null)
    {
        path ??= GetDefaultBackupPath();
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch { }
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

        try
        {
            // Check IPv6 binding on Wi-Fi adapters
            var wifiAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Select(n => n.Name)
                .ToList();

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
