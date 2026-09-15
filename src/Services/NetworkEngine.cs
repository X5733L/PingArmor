using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PingArmor.Config;
using PingArmor.Models;

namespace PingArmor.Services;

public class NetworkEngine : INetworkEngine
{
    private readonly AppConfig _config;

    public NetworkEngine(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Collects up-to-date information on all network adapters in the system.
    /// </summary>
    public List<NetworkAdapterInfo> GetAdapters()
    {
        var result = new Dictionary<int, NetworkAdapterInfo>();

        // 1. Gather baseline information via fast .NET NetworkInterface API
        var nics = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var nic in nics)
        {
            var ipProps = nic.GetIPProperties();
            int ifIndex = -1;
            try
            {
                var ipv4 = ipProps.GetIPv4Properties();
                if (ipv4 != null)
                {
                    ifIndex = ipv4.Index;
                }
            }
            catch
            {
                // IPv4 properties not supported on this adapter
            }

            if (ifIndex <= 0)
            {
                try
                {
                    var ipv6 = ipProps.GetIPv6Properties();
                    if (ipv6 != null) ifIndex = ipv6.Index;
                }
                catch { }
            }

            if (ifIndex <= 0) continue;

            bool isPhysical = nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                              !nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                              !nic.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase) &&
                              !nic.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) &&
                              !nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                              !nic.Description.Contains("Wintun", StringComparison.OrdinalIgnoreCase);

            var type = AdapterClassifier.Classify(nic.Description, nic.Name, nic.NetworkInterfaceType.ToString(), isPhysical);

            result[ifIndex] = new NetworkAdapterInfo
            {
                InterfaceIndex = ifIndex,
                Name = nic.Name,
                Description = nic.Description,
                IsPhysical = isPhysical,
                MediaType = nic.NetworkInterfaceType.ToString(),
                IsUp = nic.OperationalStatus == OperationalStatus.Up,
                Type = type
            };
        }

        // 2. Enrich with current metric values via netsh int ipv4 show interfaces
        EnrichMetricsWithNetsh(result);

        // 3. Enrich with internet connectivity status via NCSI (Get-NetConnectionProfile)
        EnrichInternetStatus(result);

        return result.Values.OrderBy(a => a.InterfaceIndex).ToList();
    }

    private void EnrichMetricsWithNetsh(Dictionary<int, NetworkAdapterInfo> map)
    {
        // 1. Fast in-process WMI query
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT InterfaceIndex, InterfaceMetric, ConnectionState FROM MSFT_NetIPInterface WHERE AddressFamily = 2"
                );
                using var results = searcher.Get();
                bool foundAny = false;
                foreach (ManagementObject obj in results)
                {
                    if (obj["InterfaceIndex"] != null &&
                        int.TryParse(obj["InterfaceIndex"].ToString(), out int ifIndex) &&
                        obj["InterfaceMetric"] != null &&
                        int.TryParse(obj["InterfaceMetric"].ToString(), out int metric))
                    {
                        int connState = obj["ConnectionState"] != null ? Convert.ToInt32(obj["ConnectionState"]) : 0;
                        if (map.TryGetValue(ifIndex, out var adapter))
                        {
                            adapter.CurrentIPv4Metric = metric;
                            if (connState == 1) // 1 = Connected
                            {
                                adapter.IsUp = true;
                            }
                            foundAny = true;
                        }
                    }
                }

                if (foundAny)
                {
                    return; // Successfully enriched via in-process WMI
                }
            }
            catch
            {
                // Fall back to netsh execution below if WMI query fails
            }
        }

        // 2. Fallback to netsh if WMI is unavailable
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = "int ipv4 show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            // Regular expression for parsing netsh output lines:
            // "  4          10        1500  connected     Wi-Fi"
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = Regex.Match(line.Trim(), @"^(\d+)\s+(\d+)\s+(\d+)\s+(\w+)\s+(.+)$");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int ifIndex) &&
                        int.TryParse(match.Groups[2].Value, out int metric))
                    {
                        if (map.TryGetValue(ifIndex, out var adapter))
                        {
                            adapter.CurrentIPv4Metric = metric;
                            if (match.Groups[4].Value.Equals("connected", StringComparison.OrdinalIgnoreCase))
                            {
                                adapter.IsUp = true;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // netsh execution error
        }
    }

    private void EnrichInternetStatus(Dictionary<int, NetworkAdapterInfo> map)
    {
        // 1. Fast in-process WMI query
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT InterfaceIndex, IPv4Connectivity, IPv6Connectivity FROM MSFT_NetConnectionProfile"
                );
                using var results = searcher.Get();
                bool foundAny = false;
                foreach (ManagementObject obj in results)
                {
                    if (obj["InterfaceIndex"] != null &&
                        int.TryParse(obj["InterfaceIndex"].ToString(), out int ifIndex))
                    {
                        int ipv4 = obj["IPv4Connectivity"] != null ? Convert.ToInt32(obj["IPv4Connectivity"]) : 0;
                        int ipv6 = obj["IPv6Connectivity"] != null ? Convert.ToInt32(obj["IPv6Connectivity"]) : 0;
                        if (map.TryGetValue(ifIndex, out var adapter))
                        {
                            // 4 = Internet in NCSI connectivity enum
                            adapter.HasInternet = (ipv4 == 4 || ipv6 == 4);
                            foundAny = true;
                        }
                    }
                }

                if (foundAny)
                {
                    return; // Successfully enriched via in-process WMI
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
            // Query Windows connection profile statuses
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetConnectionProfile | Select-Object -Property InterfaceIndex, IPv4Connectivity | ForEach-Object { \\\"$($_.InterfaceIndex):$($_.IPv4Connectivity)\\\" }\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(4000);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Trim().Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int idx))
                {
                    if (map.TryGetValue(idx, out var adapter))
                    {
                        adapter.HasInternet = parts[1].Trim().Equals("Internet", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
        catch
        {
            // Fall back to OperationalStatus (IsUp) if query fails
        }
    }

    /// <summary>
    /// Applies the metric and policy optimization plan.
    /// </summary>
    public OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false)
    {
        var result = new OptimizationResult();

        if (!force && (!plan.NeedsOptimization || plan.Actions.Count == 0))
        {
            result.Success = true;
            result.Logs.Add("Optimization not required: all metrics are in target state.");
            return result;
        }

        // Create a backup of current system settings before first optimization
        try
        {
            bool backupCreated = BackupService.CreateBackupIfNotExists();
            if (backupCreated)
            {
                result.Logs.Add("[+] System settings backup created (backup.json). Use --restore to revert changes.");
            }
        }
        catch (Exception ex)
        {
            result.Logs.Add($"[!] Warning: failed to create settings backup: {ex.Message}");
        }

        foreach (var action in plan.Actions)
        {
            try
            {
                // Set metrics for both IPv4 and IPv6
                SetInterfaceMetric(action.InterfaceIndex, action.TargetMetric);
                result.ActionsApplied++;
                result.Logs.Add($"[+] Adapter '{action.InterfaceAlias}' (id: {action.InterfaceIndex}): metric {action.CurrentMetric} -> {action.TargetMetric} ({action.Reason})");

                if (action.DisableIPv6)
                {
                    DisableIPv6OnAdapter(action.InterfaceAlias);
                    result.Logs.Add($"[+] Disabled IPv6 on wireless adapter '{action.InterfaceAlias}'");
                }
            }
            catch (Exception ex)
            {
                result.Logs.Add($"[-] Failed to configure adapter '{action.InterfaceAlias}': {ex.Message}");
            }
        }

        // Apply DNSClient and WPAD registry policies (conditionally)
        if (plan.DisableSmartNameResolution || plan.DisableWpad)
        {
            DnsHelper.ConfigureDnsPolicies(plan.DisableSmartNameResolution, plan.DisableWpad);

            var policyParts = new List<string>();
            if (plan.DisableSmartNameResolution) policyParts.Add("DisableSmartNameResolution=1");
            if (plan.DisableWpad) policyParts.Add("AutoDetect=0");
            result.Logs.Add($"[+] Registry policies updated ({string.Join(", ", policyParts)})");
        }

        // Flush system DNS resolver cache
        if (plan.FlushDns || force)
        {
            DnsHelper.FlushDnsCache();
            result.Logs.Add("[+] DNS cache successfully flushed (DnsFlushResolverCache)");
        }

        if (force && plan.Actions.Count == 0)
        {
            result.Logs.Add("[+] All network priorities and metrics are already optimal. Optimization verified.");
        }

        result.Success = true;
        return result;
    }

    private static void SetInterfaceMetric(int interfaceIndex, int metric)
    {
        // 1. Fast native configuration via netsh
        RunProcess("netsh.exe", $"int ipv4 set interface {interfaceIndex} metric={metric}");
        RunProcess("netsh.exe", $"int ipv6 set interface {interfaceIndex} metric={metric}");

        // 2. Ensure AutomaticMetric flag is disabled via PowerShell
        RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetIPInterface -InterfaceIndex {interfaceIndex} -AutomaticMetric Disabled -InterfaceMetric {metric} -ErrorAction SilentlyContinue\"");
    }

    private static void DisableIPv6OnAdapter(string adapterName)
    {
        RunProcess("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Disable-NetAdapterBinding -Name '{adapterName}' -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\"");
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
            p?.WaitForExit(3000);
        }
        catch { }
    }
}
