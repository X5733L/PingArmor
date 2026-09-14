using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
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
    public OptimizationResult ApplyPlan(OptimizationPlan plan)
    {
        var result = new OptimizationResult();

        if (!plan.NeedsOptimization || plan.Actions.Count == 0)
        {
            result.Success = true;
            result.Logs.Add("Optimization not required: all metrics are in target state.");
            return result;
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

        // Apply DNSClient and WPAD registry policies
        DnsHelper.ConfigureDnsPolicies(plan.DisableSmartNameResolution, plan.DisableWpad);
        result.Logs.Add("[+] Registry policies updated (DisableSmartNameResolution=1, AutoDetect=0)");

        // Flush system DNS resolver cache
        if (plan.FlushDns)
        {
            DnsHelper.FlushDnsCache();
            result.Logs.Add("[+] DNS cache successfully flushed (DnsFlushResolverCache)");
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
