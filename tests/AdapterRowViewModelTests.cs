using System;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.UI.ViewModels;
using Xunit;

namespace PingArmor.Tests;

public class AdapterRowViewModelTests
{
    private static readonly object TestLock = new();

    private static NetworkAdapterInfo CreateAdapter(AdapterType type = AdapterType.PhysicalEthernet)
        => new()
        {
            InterfaceIndex = 7,
            Name = "Ethernet",
            Description = "Intel I219-V",
            IsPhysical = type is AdapterType.PhysicalEthernet or AdapterType.PhysicalWiFi,
            IsUp = true,
            HasInternet = true,
            CurrentIPv4Metric = 5,
            Type = type
        };

    [Fact]
    public void IsExcluded_Set_RaisesCallback()
    {
        AdapterRowViewModel? changedRow = null;
        bool? changedValue = null;
        var row = new AdapterRowViewModel(
            CreateAdapter(),
            isExcluded: false,
            onExclusionChanged: (r, value) => { changedRow = r; changedValue = value; });

        row.IsExcluded = true;

        Assert.Same(row, changedRow);
        Assert.True(changedValue);
    }

    [Fact]
    public void IsExcluded_SetToSameValue_DoesNotRaiseCallback()
    {
        int calls = 0;
        var row = new AdapterRowViewModel(
            CreateAdapter(),
            isExcluded: true,
            onExclusionChanged: (_, _) => calls++);

        row.IsExcluded = true;

        Assert.Equal(0, calls);
    }

    [Fact]
    public void SetExcludedSilently_DoesNotRaiseCallback()
    {
        int calls = 0;
        var row = new AdapterRowViewModel(
            CreateAdapter(),
            isExcluded: false,
            onExclusionChanged: (_, _) => calls++);

        row.SetExcludedSilently(true);

        Assert.True(row.IsExcluded);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(AppLanguage.Ru, AdapterType.PhysicalEthernet, "Ethernet")]
    [InlineData(AppLanguage.Ru, AdapterType.PhysicalWiFi, "Wi-Fi")]
    [InlineData(AppLanguage.Ru, AdapterType.VirtualOrVpn, "Виртуальный / VPN")]
    [InlineData(AppLanguage.Ru, AdapterType.Other, "Прочее")]
    [InlineData(AppLanguage.En, AdapterType.VirtualOrVpn, "Virtual / VPN")]
    [InlineData(AppLanguage.En, AdapterType.Other, "Other")]
    public void TypeDisplay_IsLocalized(AppLanguage language, AdapterType type, string expected)
    {
        lock (TestLock)
        {
            LocalizationService.SetLanguage(AppLanguage.Ru);
            try
            {
                LocalizationService.SetLanguage(language);
                var row = new AdapterRowViewModel(CreateAdapter(type), isExcluded: false);
                Assert.Equal(expected, row.TypeDisplay);
            }
            finally
            {
                LocalizationService.SetLanguage(AppLanguage.Ru);
            }
        }
    }

    [Fact]
    public void MetricText_ShowsDashWhenMetricUnavailable()
    {
        var adapter = CreateAdapter();
        adapter.CurrentIPv4Metric = -1;

        var row = new AdapterRowViewModel(adapter, isExcluded: false);

        Assert.Equal("—", row.MetricText);
    }
}
