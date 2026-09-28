using System;
using System.Collections.Generic;
using System.Linq;
using PingArmor.Config;
using PingArmor.Models;

namespace PingArmor.Services;

public class MetricDecisionEngine
{
    public static OptimizationPlan Evaluate(IEnumerable<NetworkAdapterInfo> adapters, AppConfig config)
    {
        var plan = new OptimizationPlan
        {
            DisableSmartNameResolution = config.DisableSmartNameResolution,
            DisableWpad = config.DisableWpad,
            FlushDns = config.FlushDnsOnChange
        };

        var adapterList = adapters.ToList();
        var primaryAdapter = AdapterClassifier.DeterminePrimaryAdapter(adapterList);
        plan.PrimaryAdapter = primaryAdapter;

        if (primaryAdapter == null)
        {
            plan.NeedsOptimization = false;
            plan.Summary = "No active network adapters with internet access.";
            return plan;
        }

        if (!config.EnableMetricOptimization)
        {
            plan.NeedsOptimization = false;
            plan.Summary = $"Metric optimization disabled. Primary: '{primaryAdapter.Name}' (metric {primaryAdapter.CurrentIPv4Metric})";
            return plan;
        }

        int primaryTargetMetric = primaryAdapter.Type == AdapterType.PhysicalEthernet
            ? config.PrimaryEthernetMetric
            : config.PrimaryWifiMetric;

        var excludedNames = new HashSet<string>(config.GetExcludeSnapshot(), StringComparer.OrdinalIgnoreCase);

        foreach (var adapter in adapterList)
        {
            if (adapter.Name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) ||
                adapter.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (excludedNames.Contains(adapter.Name) || excludedNames.Contains(adapter.Description))
            {
                continue;
            }

            // The metric could not be read (WMI and netsh both failed). Do not pretend
            // it is optimal — surface it as a warning instead.
            if (!adapter.HasMetricData)
            {
                plan.Warnings.Add($"Adapter '{adapter.Name}' (id: {adapter.InterfaceIndex}): IPv4 metric could not be read; skipped.");
                continue;
            }

            // 1. Primary adapter
            if (adapter.InterfaceIndex == primaryAdapter.InterfaceIndex)
            {
                if (adapter.CurrentIPv4Metric != primaryTargetMetric || adapter.AutomaticMetric)
                {
                    plan.Actions.Add(new OptimizationAction
                    {
                        InterfaceIndex = adapter.InterfaceIndex,
                        InterfaceAlias = adapter.Name,
                        CurrentMetric = adapter.CurrentIPv4Metric,
                        TargetMetric = primaryTargetMetric,
                        Reason = $"Primary internet adapter ({adapter.Type})",
                        DisableIPv6 = (adapter.Type == AdapterType.PhysicalWiFi && config.DisableIPv6OnWifi)
                    });
                }
                continue;
            }

            // 2. Disconnected physical adapters
            if (adapter.IsPhysical && !adapter.IsUp)
            {
                if (adapter.CurrentIPv4Metric != config.DisconnectedAdapterMetric || adapter.AutomaticMetric)
                {
                    plan.Actions.Add(new OptimizationAction
                    {
                        InterfaceIndex = adapter.InterfaceIndex,
                        InterfaceAlias = adapter.Name,
                        CurrentMetric = adapter.CurrentIPv4Metric,
                        TargetMetric = config.DisconnectedAdapterMetric,
                        Reason = "Disconnected physical adapter"
                    });
                }
                continue;
            }

            // 3. Secondary connected physical adapters (not primary) — prevent routing conflicts
            if (adapter.IsPhysical && adapter.IsUp)
            {
                int secondaryTarget = config.SecondaryPhysicalMetric;
                if (adapter.CurrentIPv4Metric < primaryTargetMetric || adapter.AutomaticMetric)
                {
                    plan.Actions.Add(new OptimizationAction
                    {
                        InterfaceIndex = adapter.InterfaceIndex,
                        InterfaceAlias = adapter.Name,
                        CurrentMetric = adapter.CurrentIPv4Metric,
                        TargetMetric = secondaryTarget,
                        Reason = "Secondary physical adapter (lowered priority to prevent routing conflict)",
                        DisableIPv6 = (adapter.Type == AdapterType.PhysicalWiFi && config.DisableIPv6OnWifi)
                    });
                }
                continue;
            }

            // 4. Virtual, VPN, TAP adapters
            if (adapter.Type == AdapterType.VirtualOrVpn || !adapter.IsPhysical)
            {
                // Action is required if VPN metric is hijacking (<= primary target metric) OR not at target
                if (adapter.CurrentIPv4Metric <= primaryTargetMetric || adapter.CurrentIPv4Metric != config.VirtualAdapterMetric || adapter.AutomaticMetric)
                {
                    plan.Actions.Add(new OptimizationAction
                    {
                        InterfaceIndex = adapter.InterfaceIndex,
                        InterfaceAlias = adapter.Name,
                        CurrentMetric = adapter.CurrentIPv4Metric,
                        TargetMetric = config.VirtualAdapterMetric,
                        Reason = "Virtual / VPN adapter (lowered priority)"
                    });
                }
            }
        }

        plan.NeedsOptimization = plan.Actions.Count > 0;
        plan.Summary = plan.NeedsOptimization
            ? $"Optimization required for {plan.Actions.Count} adapter(s) (Primary: '{primaryAdapter.Name}', metric {primaryTargetMetric})"
            : plan.Warnings.Count > 0
                ? $"Metrics look optimal for evaluable adapters, but {plan.Warnings.Count} adapter(s) could not be read. Primary: '{primaryAdapter.Name}'"
                : $"Metrics are optimal. Primary: '{primaryAdapter.Name}' (metric {primaryTargetMetric})";

        return plan;
    }
}
