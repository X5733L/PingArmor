using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PingArmor.UI.ViewModels;

/// <summary>
/// Drives the WPF tray icon (H.NotifyIcon): status text, dynamic shield icon,
/// context menu commands and notifications.
/// </summary>
public sealed partial class TrayIconViewModel : ViewModelBase
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly AppConfig _config;
    private readonly NetworkMonitor _monitor;
    private readonly Dictionary<string, (Icon Icon, IntPtr Handle)> _icons = new();

    private OptimizationPlan? _lastPlan;
    private Action<string, string, bool>? _notify;
    private DashboardWindow? _dashboardWindow;

    [ObservableProperty]
    private bool _isNotElevated;

    [ObservableProperty]
    private Icon? _icon;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _elevateText = string.Empty;

    [ObservableProperty]
    private string _openDashboardText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _primaryAdapterText = string.Empty;

    [ObservableProperty]
    private string _optimizeText = string.Empty;

    [ObservableProperty]
    private string _toggleMonitoringText = string.Empty;

    [ObservableProperty]
    private string _exitText = string.Empty;

    [ObservableProperty]
    private string _toolTipText = string.Empty;

    [ObservableProperty]
    private SymbolRegular _monitoringIcon = SymbolRegular.Pause24;

    public TrayIconViewModel(AppConfig config, NetworkMonitor monitor)
    {
        _config = config;
        _monitor = monitor;

        InitShieldIcons();
        IsNotElevated = !Program.IsAdministrator();
        Icon = GetShieldIcon("green");

        _monitor.PlanEvaluated += OnPlanEvaluated;
        _monitor.OptimizationApplied += OnOptimizationApplied;
        _monitor.StatusChanged += OnStatusChanged;

        UpdateTexts();
        _monitor.Start();
        ApplyState();
    }

    public void AttachNotifier(Action<string, string, bool> notify) => _notify = notify;

    public void ShowDashboard(int navIndex = 0)
    {
        SafeInvoke(() =>
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new DashboardWindow(_monitor, LogService.Log);
                _dashboardWindow.Closed += (_, _) => _dashboardWindow = null;
                _dashboardWindow.UpdateOverviewState(_lastPlan);
            }

            _dashboardWindow.SelectNav(navIndex);
            _dashboardWindow.Show();

            if (_dashboardWindow.WindowState == WindowState.Minimized)
            {
                _dashboardWindow.WindowState = WindowState.Normal;
            }

            _dashboardWindow.Activate();
        });
    }

    [RelayCommand]
    private void OpenDashboard() => ShowDashboard(0);

    [RelayCommand]
    private void Optimize() => _monitor.TriggerManualCheck();

    [RelayCommand]
    private void ToggleMonitoring()
    {
        if (_monitor.IsRunning) _monitor.Stop();
        else _monitor.Start();
    }

    [RelayCommand]
    private void RestartAsAdmin()
    {
        if (!Program.TrySelfElevate(Array.Empty<string>()))
        {
            Exit();
        }
    }

    [RelayCommand]
    private void Exit()
    {
        if (_config.RestoreOnExit && !BackupService.HasRestoredOnExit)
        {
            try
            {
                BackupService.RestoreFromBackup(deleteBackupAfterRestore: false);
                BackupService.HasRestoredOnExit = true;
            }
            catch { }
        }

        WlanOptimizerService.RestoreDefaultScan();

        try
        {
            _dashboardWindow?.Close();
        }
        catch { }

        try
        {
            System.Windows.Application.Current?.Shutdown();
        }
        catch { }
    }

    private static void SafeInvoke(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    private void OnStatusChanged(bool isRunning)
    {
        SafeInvoke(() =>
        {
            _dashboardWindow?.UpdateOverviewState(_lastPlan);
            UpdateTexts();
            ApplyState();
        });
    }

    private void OnPlanEvaluated(OptimizationPlan plan)
    {
        SafeInvoke(() =>
        {
            _lastPlan = plan;
            _dashboardWindow?.UpdateOverviewState(plan);
            ApplyState();
        });
    }

    private void OnOptimizationApplied(OptimizationResult result)
    {
        SafeInvoke(() =>
        {
            var s = LocalizationService.Strings;
            Icon = result.Success ? GetShieldIcon("green") : GetShieldIcon("orange");

            if (!_config.ShowNotifications) return;

            if (result.ActionsApplied > 0)
            {
                _notify?.Invoke(s.AppTitle, string.Format(s.BalloonOptimizedFormat, result.ActionsApplied), false);
            }
            else if (!result.Success && !string.IsNullOrEmpty(result.Error))
            {
                _notify?.Invoke(s.AppTitle, result.Error!, true);
            }
        });
    }

    protected override void OnLanguageChangedCore()
    {
        UpdateTexts();
        ApplyState();
    }

    /// <summary>Updates the static (language dependent) menu texts.</summary>
    private void UpdateTexts()
    {
        var s = LocalizationService.Strings;
        HeaderText = Program.IsAdministrator() ? s.HeaderAdmin : s.HeaderNoAdmin;
        ElevateText = s.RestartAsAdmin;
        OpenDashboardText = s.OpenDashboard;
        OptimizeText = s.OptimizeNow;
        ExitText = s.Exit;
    }

    /// <summary>Recomputes tray icon, status text, tooltip and the monitoring command label.</summary>
    private void ApplyState()
    {
        var s = LocalizationService.Strings;

        ToggleMonitoringText = _monitor.IsRunning ? s.PauseProtection : s.ResumeProtection;
        MonitoringIcon = _monitor.IsRunning ? SymbolRegular.Pause24 : SymbolRegular.Play24;

        if (!_monitor.IsRunning)
        {
            StatusText = s.StatusPaused;
            PrimaryAdapterText = _lastPlan?.PrimaryAdapter is { } pausedAdapter
                ? string.Format(s.PrimaryChannelFormat, pausedAdapter.Name, pausedAdapter.CurrentIPv4Metric)
                : s.PrimaryChannelNone;
            Icon = GetShieldIcon("gray");
            SetToolTip(s.StatusPaused);
            return;
        }

        if (_lastPlan?.PrimaryAdapter is { } adapter)
        {
            PrimaryAdapterText = string.Format(s.PrimaryChannelFormat, adapter.Name, adapter.CurrentIPv4Metric);
            StatusText = _lastPlan.NeedsOptimization ? s.StatusAdjusting : s.StatusProtected;
            Icon = _lastPlan.NeedsOptimization ? GetShieldIcon("orange") : GetShieldIcon("green");
            SetToolTip(_lastPlan.NeedsOptimization ? s.StatusAdjusting : s.StatusProtected, adapter.Name);
        }
        else
        {
            PrimaryAdapterText = s.PrimaryChannelDetecting;
            StatusText = s.StatusInitializing;
            Icon = GetShieldIcon("orange");
            SetToolTip(s.StatusInitializing);
        }
    }

    private void SetToolTip(string status, string? detail = null)
    {
        string text = $"{LocalizationService.Strings.AppTitle} — {status}";
        if (!string.IsNullOrEmpty(detail))
        {
            text += $" · {detail}";
        }

        ToolTipText = text.Length <= 127 ? text : text[..127];
    }

    #region Shield icon GDI cache

    private void InitShieldIcons()
    {
        _icons["green"] = CreateShieldIcon(Color.ForestGreen);
        _icons["orange"] = CreateShieldIcon(Color.DarkOrange);
        _icons["crimson"] = CreateShieldIcon(Color.Crimson);
        _icons["gray"] = CreateShieldIcon(Color.Gray);
    }

    private Icon GetShieldIcon(string key)
        => _icons.TryGetValue(key, out var tuple) ? tuple.Icon : SystemIcons.Application;

    private static (Icon Icon, IntPtr Handle) CreateShieldIcon(Color color)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float scale = size / 16f;
            using var path = new GraphicsPath();
            path.AddLine(2 * scale, 2 * scale, 14 * scale, 2 * scale);
            path.AddLine(14 * scale, 2 * scale, 14 * scale, 8 * scale);
            path.AddBezier(14 * scale, 8 * scale, 14 * scale, 13 * scale, 8 * scale, 15 * scale, 8 * scale, 15 * scale);
            path.AddBezier(8 * scale, 15 * scale, 2 * scale, 13 * scale, 2 * scale, 8 * scale, 2 * scale, 8 * scale);
            path.CloseFigure();

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);

            using var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.0f * scale);
            g.DrawPath(pen, path);
        }

        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        return (icon, hIcon);
    }

    public override void Dispose()
    {
        base.Dispose();

        _monitor.PlanEvaluated -= OnPlanEvaluated;
        _monitor.OptimizationApplied -= OnOptimizationApplied;
        _monitor.StatusChanged -= OnStatusChanged;

        foreach (var tuple in _icons.Values)
        {
            try
            {
                tuple.Icon.Dispose();
                if (tuple.Handle != IntPtr.Zero)
                {
                    DestroyIcon(tuple.Handle);
                }
            }
            catch { }
        }
        _icons.Clear();
    }

    #endregion
}