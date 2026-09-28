using CommunityToolkit.Mvvm.ComponentModel;
using PingArmor.Localization;
using PingArmor.Models;
using Wpf.Ui.Controls;

namespace PingArmor.UI.ViewModels;

/// <summary>One network adapter row on the Adapters page.</summary>
public sealed class AdapterRowViewModel : ObservableObject
{
    private readonly Action<AdapterRowViewModel, bool>? _onExclusionChanged;
    private bool _isExcluded;
    private bool _suppressExclusionCallback;

    public AdapterRowViewModel(
        NetworkAdapterInfo adapter,
        bool isExcluded,
        Action<AdapterRowViewModel, bool>? onExclusionChanged = null)
    {
        Adapter = adapter;
        _isExcluded = isExcluded;
        _onExclusionChanged = onExclusionChanged;
    }

    public NetworkAdapterInfo Adapter { get; }

    public int InterfaceIndex => Adapter.InterfaceIndex;
    public string Name => Adapter.Name;
    public string Description => Adapter.Description;
    public bool IsPhysical => Adapter.IsPhysical;
    public bool HasInternet => Adapter.HasInternet;

    public string GroupTitle => IsPhysical
        ? LocalizationService.Strings.AdaptersGroupPhysical
        : LocalizationService.Strings.AdaptersGroupVirtual;

    public string TypeDisplay => AdaptersViewModel.LocalizedType(Adapter.Type);

    public string StatusText => Adapter.IsUp
        ? LocalizationService.Strings.AdaptersActiveTag
        : LocalizationService.Strings.AdaptersDisconnectedTag;

    public string MetricText => Adapter.HasMetricData
        ? Adapter.CurrentIPv4Metric.ToString()
        : "—";

    public string InternetText => Adapter.HasInternet
        ? LocalizationService.Strings.InternetYes
        : LocalizationService.Strings.InternetNo;

    public SymbolRegular TypeSymbol => Adapter.Type switch
    {
        AdapterType.PhysicalEthernet => SymbolRegular.PlugConnected24,
        AdapterType.PhysicalWiFi => SymbolRegular.Wifi124,
        AdapterType.VirtualOrVpn => SymbolRegular.Shield24,
        _ => SymbolRegular.Desktop24
    };

    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (!SetProperty(ref _isExcluded, value)) return;
            if (!_suppressExclusionCallback)
            {
                _onExclusionChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>Refreshes localized/computed text without rebuilding the row.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(GroupTitle));
        OnPropertyChanged(nameof(TypeDisplay));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(InternetText));
        OnPropertyChanged(nameof(MetricText));
    }

    /// <summary>Used when the view model rebuilds the row set to avoid firing config writes.</summary>
    public void SetExcludedSilently(bool value)
    {
        _suppressExclusionCallback = true;
        try
        {
            IsExcluded = value;
        }
        finally
        {
            _suppressExclusionCallback = false;
        }
    }
}