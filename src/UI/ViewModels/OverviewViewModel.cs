using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingArmor.Config;
using PingArmor.Models;
using PingArmor.Services;

namespace PingArmor.UI.ViewModels;

public enum OverviewStatus
{
    Initializing,
    Protected,
    Adjusting,
    Paused
}

/// <summary>View model for the overview (home) page.</summary>
public sealed partial class OverviewViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly NetworkMonitor _monitor;

    private OptimizationPlan? _lastPlan;
    private DateTime? _lastCheckedAt;
    private OptimizationResult? _lastResult;
    private DateTime? _lastResultAt;

    public ObservableCollection<PlannedChangeViewModel> PlannedChanges { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    [ObservableProperty]
    private OverviewStatus _status = OverviewStatus.Initializing;

    [ObservableProperty]
    private string _statusTitle = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _primaryAdapterName = string.Empty;

    [ObservableProperty]
    private string _primaryAdapterDetails = string.Empty;

    [ObservableProperty]
    private string _wlanStatusText = string.Empty;

    [ObservableProperty]
    private string _checkedAtText = string.Empty;

    [ObservableProperty]
    private bool _needsOptimization;

    [ObservableProperty]
    private string _lastOptimizationText = string.Empty;

    public bool HasPlannedChanges => PlannedChanges.Count > 0;
    public bool HasNoPlannedChanges => PlannedChanges.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasLastOptimization => !string.IsNullOrEmpty(LastOptimizationText);
    public string WarningsText => string.Join(Environment.NewLine, Warnings);

    public string ProtectionButtonText => _monitor.IsRunning ? Texts.PauseProtection : Texts.ResumeProtection;
    public Wpf.Ui.Controls.SymbolRegular ProtectionIcon => _monitor.IsRunning
        ? Wpf.Ui.Controls.SymbolRegular.Pause24
        : Wpf.Ui.Controls.SymbolRegular.Play24;

    public OverviewViewModel(AppConfig config, NetworkMonitor monitor)
    {
        _config = config;
        _monitor = monitor;
    }

    /// <summary>Applies the latest optimization plan (or the previous one when null).</summary>
    public void Update(OptimizationPlan? plan)
    {
        if (plan != null)
        {
            _lastPlan = plan;
            _lastCheckedAt = DateTime.Now;
        }

        var p = _lastPlan;
        var s = Texts;

        if (!_monitor.IsRunning)
        {
            NeedsOptimization = false;
            Status = OverviewStatus.Paused;
            StatusTitle = s.StatusPaused;
            Summary = s.OverviewPausedDesc;
        }
        else if (p == null)
        {
            Status = OverviewStatus.Initializing;
            StatusTitle = s.StatusInitializing;
            Summary = string.Empty;
        }
        else
        {
            NeedsOptimization = p.NeedsOptimization;
            Status = p.NeedsOptimization ? OverviewStatus.Adjusting : OverviewStatus.Protected;
            StatusTitle = p.NeedsOptimization ? s.StatusAdjusting : s.StatusProtected;
            Summary = p.Summary;
        }

        if (p?.PrimaryAdapter is { } adapter)
        {
            PrimaryAdapterName = adapter.Name;
            PrimaryAdapterDetails = string.Format(
                s.PrimaryAdapterDetailsFormat,
                AdaptersViewModel.LocalizedType(adapter.Type),
                adapter.CurrentIPv4Metric,
                adapter.HasInternet ? s.InternetYes : s.InternetNo);
        }
        else
        {
            PrimaryAdapterName = s.PrimaryChannelNone;
            PrimaryAdapterDetails = s.TrayTextNoConnection;
        }

        CheckedAtText = _lastCheckedAt.HasValue
            ? string.Format(s.CheckedAtFormat, _lastCheckedAt.Value.ToString("HH:mm:ss"))
            : string.Empty;

        PlannedChanges.Clear();
        if (p != null)
        {
            foreach (var action in p.Actions)
            {
                PlannedChanges.Add(new PlannedChangeViewModel(
                    $"{action.InterfaceAlias}   {action.CurrentMetric} → {action.TargetMetric}",
                    action.Reason));
            }
        }
        OnPropertyChanged(nameof(HasPlannedChanges));
        OnPropertyChanged(nameof(HasNoPlannedChanges));

        Warnings.Clear();
        if (p != null)
        {
            foreach (var warning in p.Warnings)
            {
                Warnings.Add(warning);
            }
        }
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(WarningsText));

        WlanStatusText = _config.EnableWlanOptimizer ? s.WlanOptimizerActive : s.WlanOptimizerStandard;

        OnPropertyChanged(nameof(ProtectionButtonText));
        OnPropertyChanged(nameof(ProtectionIcon));
    }

    /// <summary>Records the outcome of the last applied optimization.</summary>
    public void SetLastResult(OptimizationResult result)
    {
        _lastResult = result;
        _lastResultAt = DateTime.Now;
        RefreshLastOptimizationText();
    }

    [RelayCommand]
    private void Optimize() => _monitor.TriggerManualCheck();

    [RelayCommand]
    private void ApplyPlan() => _monitor.TriggerManualCheck();

    [RelayCommand]
    private void ToggleProtection()
    {
        if (_monitor.IsRunning) _monitor.Stop();
        else _monitor.Start();
        Update(null);
    }

    protected override void OnLanguageChangedCore()
    {
        Update(null);
        RefreshLastOptimizationText();
    }

    private void RefreshLastOptimizationText()
    {
        LastOptimizationText = _lastResult != null && _lastResultAt.HasValue
            ? string.Format(Texts.LastOptimizationFormat, _lastResult.ActionsApplied, _lastResultAt.Value.ToString("HH:mm"))
            : string.Empty;
        OnPropertyChanged(nameof(HasLastOptimization));
    }
}