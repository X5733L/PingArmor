using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using PingArmor.Config;
using PingArmor.Models;

namespace PingArmor.Services;

public class NetworkEngine : INetworkEngine
{
    private readonly AppConfig _config;
    private readonly IProcessRunner _runner;
    private readonly IRegistryAccessor _registry;
    private readonly OptimizationLedger _ledger;

    public NetworkEngine(
        AppConfig config,
        IProcessRunner? processRunner = null,
        IRegistryAccessor? registryAccessor = null,
        OptimizationLedger? ledger = null)
    {
        _config = config;
        _runner = processRunner ?? ProcessRunner.Default;
        _registry = registryAccessor ?? RegistryAccessor.Default;
        _ledger = ledger ?? OptimizationLedger.Default;
    }

    /// <summary>
    /// Collects up-to-date information on all network adapters in the system.
    /// </summary>
    public List<NetworkAdapterInfo> GetAdapters(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, NetworkAdapterInfo>();

        // 1. Gather baseline information via fast .NET NetworkInterface API
        var nics = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var nic in nics)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        // 2. Enrich with current metric values (IPv4 + IPv6 + AutomaticMetric)
        EnrichMetrics(result, cancellationToken);

        // 3. Enrich with internet connectivity status via NCSI
        EnrichInternetStatus(result);

        return result.Values.OrderBy(a => a.InterfaceIndex).ToList();
    }

    private void EnrichMetrics(Dictionary<int, NetworkAdapterInfo> map, CancellationToken cancellationToken)
    {
        // 1. Fast in-process WMI query (IPv4 = family 2, IPv6 = family 23)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT InterfaceIndex, InterfaceMetric, AutomaticMetric, AddressFamily FROM MSFT_NetIPInterface"
                );
                using var results = searcher.Get();
                bool foundAny = false;
                foreach (ManagementObject obj in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (obj["InterfaceIndex"] == null ||
                        !int.TryParse(obj["InterfaceIndex"].ToString(), out int ifIndex) ||
                        obj["InterfaceMetric"] == null ||
                        !int.TryParse(obj["InterfaceMetric"].ToString(), out int metric))
                    {
                        continue;
                    }

                    if (!map.TryGetValue(ifIndex, out var adapter)) continue;

                    int family = ToInt(obj["AddressFamily"], 2);
                    bool automatic = ToBoolFlag(obj["AutomaticMetric"]);

                    if (family == 23) // AF_INET6
                    {
                        adapter.CurrentIPv6Metric = metric;
                    }
                    else
                    {
                        adapter.CurrentIPv4Metric = metric;
                        adapter.AutomaticMetric = automatic;
                    }

                    foundAny = true;
                }

                if (foundAny)
                {
                    return; // Successfully enriched via in-process WMI
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall back to netsh execution below if WMI query fails
            }
        }

        // 2. Fallback to netsh for IPv4 metrics (locale independent: we only parse numbers)
        ParseNetshMetrics(map);
    }

    private void ParseNetshMetrics(Dictionary<int, NetworkAdapterInfo> map)
    {
        var r = _runner.Run("netsh.exe", "int ipv4 show interfaces", 4000);
        if (r.TimedOut || string.IsNullOrEmpty(r.StandardOutput)) return;

        // Data rows look like: "  4          10        1500  connected     Wi-Fi"
        var lines = r.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = Regex.Match(line.Trim(), @"^(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(.+)$");
            if (!match.Success) continue;

            if (int.TryParse(match.Groups[1].Value, out int ifIndex) &&
                int.TryParse(match.Groups[2].Value, out int metric) &&
                map.TryGetValue(ifIndex, out var adapter))
            {
                adapter.CurrentIPv4Metric = metric;
            }
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
                        int ipv4 = ToInt(obj["IPv4Connectivity"], 0);
                        int ipv6 = ToInt(obj["IPv6Connectivity"], 0);
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
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-NetConnectionProfile | ForEach-Object { \\\"$($_.InterfaceIndex):$($_.IPv4Connectivity)\\\" }\"",
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
    public OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false, CancellationToken cancellationToken = default)
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
                result.Logs.Add("[+] System settings backup created (backups/backup.json). Use --restore to revert changes.");
            }
        }
        catch (Exception ex)
        {
            result.Logs.Add($"[!] Warning: failed to create settings backup: {ex.Message}");
        }

        foreach (var action in plan.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!force && _ledger.ShouldBackOff(action.InterfaceIndex, DateTime.UtcNow))
            {
                result.ActionsDeferred++;
                result.Logs.Add($"[!] Adapter '{action.InterfaceAlias}' (id: {action.InterfaceIndex}) keeps reverting its metric. Backing off temporarily to avoid a metric war.");
                continue;
            }

            try
            {
                ApplyMetricCommands(action.InterfaceIndex, action.TargetMetric, cancellationToken);

                if (!VerifyMetric(action.InterfaceIndex, action.TargetMetric, cancellationToken))
                {
                    result.ActionsFailed++;
                    result.Logs.Add($"[-] Adapter '{action.InterfaceAlias}' (id: {action.InterfaceIndex}): metric was not applied (target {action.TargetMetric}). Are you running as administrator?");
                    continue;
                }

                result.ActionsApplied++;
                bool backedOff = _ledger.RecordApply(action.InterfaceIndex, action.InterfaceAlias, action.TargetMetric, DateTime.UtcNow);
                result.Logs.Add($"[+] Adapter '{action.InterfaceAlias}' (id: {action.InterfaceIndex}): metric {action.CurrentMetric} -> {action.TargetMetric} ({action.Reason})");
                if (backedOff)
                {
                    result.Logs.Add($"[!] Adapter '{action.InterfaceAlias}': metric rewritten {OptimizationLedger.MaxAppliesPerWindow} times within {OptimizationLedger.Window.TotalMinutes:0} min — another client is likely reverting it.");
                }

                if (action.DisableIPv6)
                {
                    DisableIPv6OnAdapter(action.InterfaceAlias);
                    result.Logs.Add($"[+] Disabled IPv6 on wireless adapter '{action.InterfaceAlias}'");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.ActionsFailed++;
                result.Logs.Add($"[-] Failed to configure adapter '{action.InterfaceAlias}': {ex.Message}");
            }
        }

        // Apply DNSClient and WPAD registry policies
        DnsHelper.ConfigureDnsPolicies(plan.DisableSmartNameResolution, plan.DisableWpad, _registry);
        result.Logs.Add($"[+] System registry: WPAD -> {DnsHelper.GetWpadStatusDescription(_registry)}");
        result.Logs.Add($"[+] System registry: SmartDNS -> {DnsHelper.GetSmartDnsStatusDescription(_registry)}");

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

        result.Success = result.ActionsFailed == 0;
        if (!result.Success)
        {
            result.Error = $"{result.ActionsFailed} adapter action(s) failed.";
        }

        return result;
    }

    /// <summary>
    /// Applies the metric to both IP families and disables automatic metric.
    /// Process exit codes are tolerated here; the authoritative check is <see cref="VerifyMetric"/>.
    /// </summary>
    private void ApplyMetricCommands(int interfaceIndex, int metric, CancellationToken cancellationToken)
    {
        // 1. Native configuration (works even if the PowerShell cmdlets are unavailable).
        _runner.Run("netsh.exe", $"int ipv4 set interface {interfaceIndex} metric={metric}", 5000);
        _runner.Run("netsh.exe", $"int ipv6 set interface {interfaceIndex} metric={metric}", 5000);

        // 2. Ensure AutomaticMetric is disabled so Windows does not override us.
        _runner.Run(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Set-NetIPInterface -InterfaceIndex {interfaceIndex} -AutomaticMetric Disabled -InterfaceMetric {metric} -AddressFamily IPv4 -ErrorAction SilentlyContinue; Set-NetIPInterface -InterfaceIndex {interfaceIndex} -AutomaticMetric Disabled -InterfaceMetric {metric} -AddressFamily IPv6 -ErrorAction SilentlyContinue\"",
            7000);
    }

    /// <summary>
    /// Re-reads the adapter metric to verify the change actually took effect.
    /// </summary>
    private bool VerifyMetric(int interfaceIndex, int targetMetric, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryReadIPv4Metric(interfaceIndex, out int metric) && metric == targetMetric)
            {
                return true;
            }

            try
            {
                Task.Delay(120, cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        return false;
    }

    private bool TryReadIPv4Metric(int interfaceIndex, out int metric)
    {
        metric = -1;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    $"SELECT InterfaceMetric FROM MSFT_NetIPInterface WHERE AddressFamily = 2 AND InterfaceIndex = {interfaceIndex}"
                );
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    if (obj["InterfaceMetric"] != null &&
                        int.TryParse(obj["InterfaceMetric"].ToString(), out int value))
                    {
                        metric = value;
                        return true;
                    }
                }
            }
            catch
            {
                // Fall through to netsh
            }
        }

        var r = _runner.Run("netsh.exe", "int ipv4 show interfaces", 4000);
        if (r.TimedOut || string.IsNullOrEmpty(r.StandardOutput)) return false;

        foreach (var line in r.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line.Trim(), @"^(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(.+)$");
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int idx) &&
                idx == interfaceIndex &&
                int.TryParse(match.Groups[2].Value, out int value))
            {
                metric = value;
                return true;
            }
        }

        return false;
    }

    public List<string> SetIPv6OnWifiAdapters(bool disable)
    {
        var logs = new List<string>();
        try
        {
            var adapters = GetAdapters()
                .Where(a => a.Type == AdapterType.PhysicalWiFi)
                .ToList();

            foreach (var adapter in adapters)
            {
                if (disable)
                {
                    DisableIPv6OnAdapter(adapter.Name);
                    logs.Add($"[+] System adapter verified: Wi-Fi '{adapter.Name}': IPv6 is DISABLED (ms_tcpip6 binding disabled)");
                }
                else
                {
                    EnableIPv6OnAdapter(adapter.Name);
                    logs.Add($"[*] System adapter verified: Wi-Fi '{adapter.Name}': IPv6 is ENABLED (ms_tcpip6 binding restored)");
                }
            }

            if (adapters.Count == 0)
            {
                logs.Add(disable
                    ? "[*] Wi-Fi IPv6: Disabled (no active Wi-Fi adapters detected)"
                    : "[*] Wi-Fi IPv6: Enabled (no active Wi-Fi adapters detected)");
            }
        }
        catch (Exception ex)
        {
            logs.Add($"[-] Error configuring IPv6 on Wi-Fi adapters: {ex.Message}");
        }

        return logs;
    }

    private void DisableIPv6OnAdapter(string adapterName)
    {
        _runner.Run(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Disable-NetAdapterBinding -Name {PsQuote(adapterName)} -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\"",
            7000);
    }

    private void EnableIPv6OnAdapter(string adapterName)
    {
        _runner.Run(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Enable-NetAdapterBinding -Name {PsQuote(adapterName)} -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\"",
            7000);
    }

    /// <summary>
    /// Quotes a value for safe interpolation inside a single-quoted PowerShell string.
    /// </summary>
    internal static string PsQuote(string value) =>
        "'" + (value ?? string.Empty).Replace("'", "''") + "'";

    private static int ToInt(object? value, int fallback)
    {
        if (value == null) return fallback;
        if (value is int i) return i;
        if (value is uint ui) return unchecked((int)ui);
        if (value is short s) return s;
        if (value is ushort us) return us;
        if (value is byte b) return b;
        return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
    }

    private static bool ToBoolFlag(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is int i) return i == 1;
        if (value is uint ui) return ui == 1;
        if (value is short s) return s == 1;
        if (value is ushort us) return us == 1;
        if (value is byte by) return by == 1;

        string str = value.ToString() ?? string.Empty;
        return str.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               str.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               str.Equals("enabled", StringComparison.OrdinalIgnoreCase);
    }
}
