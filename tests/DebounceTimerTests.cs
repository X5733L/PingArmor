using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PingArmor.Config;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class DebounceTimerTests
{
    private class FakeNetworkEngine : INetworkEngine
    {
        public List<NetworkAdapterInfo> GetAdapters()
        {
            return new List<NetworkAdapterInfo>
            {
                new NetworkAdapterInfo
                {
                    InterfaceIndex = 4,
                    Name = "Wi-Fi",
                    Type = AdapterType.PhysicalWiFi,
                    IsPhysical = true,
                    IsUp = true,
                    HasInternet = true,
                    CurrentIPv4Metric = 10
                }
            };
        }

        public OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false)
        {
            return new OptimizationResult { Success = true };
        }
    }

    [Fact]
    public async Task ScheduleEvaluation_ShouldDebounceMultipleRapidCalls()
    {
        var config = new AppConfig();
        var fakeEngine = new FakeNetworkEngine();
        using var monitor = new NetworkMonitor(fakeEngine, config);
        
        int evaluateCount = 0;
        monitor.PlanEvaluated += (plan) =>
        {
            Interlocked.Increment(ref evaluateCount);
        };

        monitor.Start();

        // Reset counter after initial run
        await Task.Delay(150);
        Interlocked.Exchange(ref evaluateCount, 0);

        // Trigger 5 rapid requests with 100ms debounce
        for (int i = 0; i < 5; i++)
        {
            monitor.ScheduleEvaluation(100);
            await Task.Delay(20);
        }

        // Wait for debounce period to expire
        await Task.Delay(250);

        // Must execute exactly once
        Assert.Equal(1, evaluateCount);
    }

    [Fact]
    public async Task TriggerManualCheck_WhenNoOptimizationNeeded_LogsFeedbackAndAppliesForcePlan()
    {
        var config = new AppConfig();
        var fakeEngine = new FakeNetworkEngine();
        using var monitor = new NetworkMonitor(fakeEngine, config);

        var logs = new List<string>();
        bool optimizationApplied = false;

        monitor.LogMessage += (msg) => logs.Add(msg);
        monitor.OptimizationApplied += (res) => optimizationApplied = true;

        monitor.Start();
        await Task.Delay(150); // wait for initial check
        logs.Clear();
        optimizationApplied = false;

        monitor.TriggerManualCheck();
        await Task.Delay(150);

        Assert.True(optimizationApplied);
        Assert.Contains(logs, l => l.Contains("Manual network optimization requested"));
        Assert.Contains(logs, l => l.Contains("Metrics are optimal"));
    }
}
