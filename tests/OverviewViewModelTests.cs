using System;
using System.Collections.Generic;
using System.Threading;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;
using PingArmor.UI.ViewModels;
using Xunit;

namespace PingArmor.Tests;

public class OverviewViewModelTests
{
    private sealed class FakeNetworkEngine : INetworkEngine
    {
        public List<NetworkAdapterInfo> GetAdapters(CancellationToken cancellationToken = default) => new();
        public OptimizationResult ApplyPlan(OptimizationPlan plan, bool force = false, CancellationToken cancellationToken = default) => new() { Success = true };
        public List<string> SetIPv6OnWifiAdapters(bool disable) => new();
    }

    private static OverviewViewModel CreateViewModel(out NetworkMonitor monitor)
    {
        var config = new AppConfig();
        monitor = new NetworkMonitor(new FakeNetworkEngine(), config);
        return new OverviewViewModel(config, monitor);
    }

    private static OptimizationPlan CreatePlan()
    {
        var plan = new OptimizationPlan
        {
            NeedsOptimization = true,
            Summary = "summary",
            PrimaryAdapter = new NetworkAdapterInfo
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
        plan.Actions.Add(new OptimizationAction
        {
            InterfaceAlias = "Wi-Fi",
            CurrentMetric = 40,
            TargetMetric = 10,
            Reason = "reason"
        });
        plan.Warnings.Add("warning");
        return plan;
    }

    [Fact]
    public void Update_PopulatesPlannedChangesWarningsAndPrimaryAdapter()
    {
        lock (TestSync.Localization)
        {
            LocalizationService.SetLanguage(AppLanguage.Ru);
            var vm = CreateViewModel(out var monitor);
            try
            {
                vm.Update(CreatePlan());

                Assert.Single(vm.PlannedChanges);
                Assert.Contains("Wi-Fi", vm.PlannedChanges[0].Transition);
                Assert.Contains("40", vm.PlannedChanges[0].Transition);
                Assert.Contains("10", vm.PlannedChanges[0].Transition);
                Assert.True(vm.HasPlannedChanges);
                Assert.False(vm.HasNoPlannedChanges);
                Assert.True(vm.HasWarnings);
                Assert.Equal("warning", vm.WarningsText);
                Assert.Equal("Wi-Fi", vm.PrimaryAdapterName);
            }
            finally
            {
                monitor.Dispose();
                LocalizationService.SetLanguage(AppLanguage.Ru);
            }
        }
    }

    [Fact]
    public void Update_WhilePaused_ReportsPausedAndHidesPlannedChangesApply()
    {
        lock (TestSync.Localization)
        {
            LocalizationService.SetLanguage(AppLanguage.Ru);
            var vm = CreateViewModel(out var monitor);
            try
            {
                vm.Update(CreatePlan());

                Assert.Equal(OverviewStatus.Paused, vm.Status);
                Assert.False(vm.NeedsOptimization);
            }
            finally
            {
                monitor.Dispose();
            }
        }
    }

    [Fact]
    public void SetLastResult_EnablesLastOptimizationText()
    {
        lock (TestSync.Localization)
        {
            LocalizationService.SetLanguage(AppLanguage.Ru);
            var vm = CreateViewModel(out var monitor);
            try
            {
                Assert.False(vm.HasLastOptimization);

                vm.SetLastResult(new OptimizationResult { Success = true, ActionsApplied = 2 });

                Assert.True(vm.HasLastOptimization);
                Assert.Contains("2", vm.LastOptimizationText);
            }
            finally
            {
                monitor.Dispose();
            }
        }
    }
}
