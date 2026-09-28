using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;

namespace PingArmor.UI.ViewModels;

/// <summary>View model for the network adapters page.</summary>
public sealed partial class AdaptersViewModel : ViewModelBase
{
    private readonly INetworkEngine _engine;
    private readonly AppConfig _config;
    private readonly NetworkMonitor _monitor;
    private readonly Action<string> _logAppender;

    public ObservableCollection<AdapterRowViewModel> Adapters { get; } = new();

    public ICollectionView AdaptersView { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _filterText = string.Empty;

    public bool IsEmpty => !IsBusy && Adapters.Count == 0;

    public bool HasAdapters => Adapters.Count > 0;

    public AdaptersViewModel(INetworkEngine engine, AppConfig config, NetworkMonitor monitor, Action<string> logAppender)
    {
        _engine = engine;
        _config = config;
        _monitor = monitor;
        _logAppender = logAppender;

        AdaptersView = CollectionViewSource.GetDefaultView(Adapters);
        AdaptersView.Filter = MatchesFilter;
        AdaptersView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(AdapterRowViewModel.GroupTitle)));
        AdaptersView.SortDescriptions.Add(
            new SortDescription(nameof(AdapterRowViewModel.IsPhysical), ListSortDirection.Descending));
        AdaptersView.SortDescriptions.Add(
            new SortDescription(nameof(AdapterRowViewModel.Name), ListSortDirection.Ascending));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            var adapters = await Task.Run(() => _engine.GetAdapters());
            var excluded = new HashSet<string>(
                _config.GetExcludeSnapshot(),
                StringComparer.OrdinalIgnoreCase);

            Adapters.Clear();
            foreach (var adapter in adapters)
            {
                bool isExcluded =
                    excluded.Contains(adapter.Name) || excluded.Contains(adapter.Description);
                Adapters.Add(new AdapterRowViewModel(adapter, isExcluded, OnExclusionChanged));
            }
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to load adapters: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasAdapters));
        }
    }

    /// <summary>Called by a row when the user toggles its exclusion checkbox.</summary>
    private void OnExclusionChanged(AdapterRowViewModel row, bool isExcluded)
    {
        if (string.IsNullOrEmpty(row.Name)) return;

        if (isExcluded)
        {
            if (_config.AddExclusion(row.Name))
            {
                _logAppender($"[*] Adapter '{row.Name}' added to exclusions.");
            }
        }
        else
        {
            _config.RemoveExclusion(row.Name);
            _logAppender($"[*] Adapter '{row.Name}' removed from exclusions.");
        }

        try
        {
            _config.Save();
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to save configuration: {ex.Message}");
        }

        _monitor.TriggerManualCheck();
    }

    protected override void OnLanguageChangedCore()
    {
        foreach (var row in Adapters)
        {
            row.RefreshTexts();
        }
    }

    private bool MatchesFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(FilterText)) return true;
        if (item is not AdapterRowViewModel row) return false;

        string term = FilterText.Trim();
        return row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.TypeDisplay.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnFilterTextChanged(string value) => AdaptersView?.Refresh();

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    internal static string LocalizedType(AdapterType type) => type switch
    {
        AdapterType.PhysicalEthernet => LocalizationService.Strings.AdapterTypeEthernet,
        AdapterType.PhysicalWiFi => LocalizationService.Strings.AdapterTypeWiFi,
        AdapterType.VirtualOrVpn => LocalizationService.Strings.AdapterTypeVirtual,
        _ => LocalizationService.Strings.AdapterTypeOther
    };
}