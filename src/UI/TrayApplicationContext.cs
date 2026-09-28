using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PingArmor.Common;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;

namespace PingArmor.UI;

public class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly AppConfig _config;
    private readonly NetworkEngine _engine;
    private readonly NetworkMonitor _monitor;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _headerMenuItem;
    private readonly ToolStripMenuItem? _elevateItem;
    private readonly ToolStripMenuItem _dashboardMenuItem;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _primaryAdapterMenuItem;
    private readonly ToolStripMenuItem _optimizeNowItem;
    private readonly ToolStripMenuItem _toggleMonitoringMenuItem;
    private readonly ToolStripMenuItem _exitItem;

    private DashboardWindow? _dashboardWindow;
    private readonly Dictionary<string, (Icon Icon, IntPtr Handle)> _shieldIcons = new();
    private OptimizationPlan? _lastPlan;

    public TrayApplicationContext(AppConfig config, NetworkEngine engine, NetworkMonitor monitor)
    {
        _config = config;
        _engine = engine;
        _monitor = monitor;

        // Composition root for XAML-declared views (must be ready before any window is built).
        AppServices.Initialize(_config, _engine, _monitor, AppendLog);

        // Initialize language from configuration
        LocalizationService.SetLanguage(_config.Language);

        // Cache shield icons to prevent GDI descriptor leaks
        InitShieldIcons();

        _contextMenu = new ContextMenuStrip();

        bool isAdmin = Program.IsAdministrator();
        var strings = LocalizationService.Strings;

        _headerMenuItem = new ToolStripMenuItem(isAdmin ? strings.HeaderAdmin : strings.HeaderNoAdmin)
        {
            Enabled = false,
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };

        if (!isAdmin)
        {
            _elevateItem = new ToolStripMenuItem(strings.RestartAsAdmin, null, (s, e) =>
            {
                if (!Program.TrySelfElevate(Array.Empty<string>()))
                {
                    ExitApplication();
                }
            })
            {
                ForeColor = Color.Firebrick,
                Font = new Font(Control.DefaultFont, FontStyle.Bold)
            };
        }

        _dashboardMenuItem = new ToolStripMenuItem(strings.OpenDashboard, null, (s, e) => ShowDashboard())
        {
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };

        _statusMenuItem = new ToolStripMenuItem(strings.StatusInitializing) { Enabled = false };
        _primaryAdapterMenuItem = new ToolStripMenuItem(strings.PrimaryChannelDetecting) { Enabled = false };

        _optimizeNowItem = new ToolStripMenuItem(strings.OptimizeNow, null, (s, e) => _monitor.TriggerManualCheck());
        _toggleMonitoringMenuItem = new ToolStripMenuItem(strings.PauseProtection, null, OnToggleMonitoring);
        _exitItem = new ToolStripMenuItem(strings.Exit, null, (s, e) => ExitApplication());

        _contextMenu.Items.Add(_headerMenuItem);
        if (_elevateItem != null)
        {
            _contextMenu.Items.Add(_elevateItem);
        }
        _contextMenu.Items.Add(_dashboardMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_statusMenuItem);
        _contextMenu.Items.Add(_primaryAdapterMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_optimizeNowItem);
        _contextMenu.Items.Add(_toggleMonitoringMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = strings.AppTitle,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Icon = GetShieldIcon("green")
        };

        _notifyIcon.DoubleClick += (s, e) => ShowDashboard();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowDashboard();
            }
        };

        // Subscribe to language changes for instant UI refresh
        LocalizationService.LanguageChanged += UpdateLocalization;

        // Subscribe to monitor events
        _monitor.PlanEvaluated += OnPlanEvaluated;
        _monitor.OptimizationApplied += OnOptimizationApplied;
        _monitor.LogMessage += AppendLog;
        _monitor.StatusChanged += OnStatusChanged;

        // Ensure system settings backup exists before monitoring starts
        try
        {
            if (BackupService.CreateBackupIfNotExists())
            {
                AppendLog(strings.BackupCreatedToast);
            }
        }
        catch { }

        _monitor.Start();
    }

    private void UpdateLocalization()
    {
        var s = LocalizationService.Strings;
        bool isAdmin = Program.IsAdministrator();

        _dashboardMenuItem.Text = s.OpenDashboard;
        _headerMenuItem.Text = isAdmin ? s.HeaderAdmin : s.HeaderNoAdmin;
        if (_elevateItem != null)
        {
            _elevateItem.Text = s.RestartAsAdmin;
        }

        _optimizeNowItem.Text = s.OptimizeNow;
        _toggleMonitoringMenuItem.Text = _monitor.IsRunning ? s.PauseProtection : s.ResumeProtection;
        _exitItem.Text = s.Exit;

        // Update current status menu items
        if (!_monitor.IsRunning)
        {
            _statusMenuItem.Text = s.StatusPaused;
        }
        else if (_lastPlan != null)
        {
            RenderPlanStatus(_lastPlan);
        }
        else
        {
            _statusMenuItem.Text = s.StatusInitializing;
            _primaryAdapterMenuItem.Text = s.PrimaryChannelDetecting;
        }
    }

    private void OnToggleMonitoring(object? sender, EventArgs e)
    {
        if (_monitor.IsRunning)
        {
            _monitor.Stop();
        }
        else
        {
            _monitor.Start();
        }
    }

    private void OnStatusChanged(bool isRunning)
    {
        SafeInvoke(() =>
        {
            var s = LocalizationService.Strings;
            _toggleMonitoringMenuItem.Text = isRunning ? s.PauseProtection : s.ResumeProtection;
            _dashboardWindow?.UpdateOverviewState(_lastPlan);

            if (!isRunning)
            {
                _notifyIcon.Icon = GetShieldIcon("gray");
                _statusMenuItem.Text = s.StatusPaused;
            }
        });
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

    private void OnPlanEvaluated(OptimizationPlan plan)
    {
        SafeInvoke(() =>
        {
            _lastPlan = plan;
            _dashboardWindow?.UpdateOverviewState(plan);
            RenderPlanStatus(plan);
        });
    }

    private void RenderPlanStatus(OptimizationPlan plan)
    {
        var s = LocalizationService.Strings;
        if (plan.PrimaryAdapter != null)
        {
            _primaryAdapterMenuItem.Text = string.Format(s.PrimaryChannelFormat, plan.PrimaryAdapter.Name, plan.PrimaryAdapter.CurrentIPv4Metric);
            _statusMenuItem.Text = plan.NeedsOptimization
                ? s.StatusAdjusting
                : s.StatusProtected;

            _notifyIcon.Icon = plan.NeedsOptimization
                ? GetShieldIcon("orange")
                : GetShieldIcon("green");

            _notifyIcon.Text = $"PingArmor: {plan.PrimaryAdapter.Name}";
        }
        else
        {
            _primaryAdapterMenuItem.Text = s.PrimaryChannelNone;
            _statusMenuItem.Text = s.StatusNoConnection;
            _notifyIcon.Icon = GetShieldIcon("crimson");
            _notifyIcon.Text = s.TrayTextNoConnection;
        }
    }

    private void OnOptimizationApplied(OptimizationResult result)
    {
        SafeInvoke(() =>
        {
            var s = LocalizationService.Strings;
            _notifyIcon.Icon = result.Success ? GetShieldIcon("green") : GetShieldIcon("orange");

            if (_config.ShowNotifications && result.ActionsApplied > 0)
            {
                _notifyIcon.ShowBalloonTip(
                    3000,
                    s.AppTitle,
                    string.Format(s.BalloonOptimizedFormat, result.ActionsApplied),
                    ToolTipIcon.Info
                );
            }
            else if (_config.ShowNotifications && !result.Success && !string.IsNullOrEmpty(result.Error))
            {
                _notifyIcon.ShowBalloonTip(
                    3000,
                    s.AppTitle,
                    result.Error!,
                    ToolTipIcon.Warning
                );
            }
        });
    }

    private void AppendLog(string message)
    {
        LogService.Log(message);
    }

    public void ShowDashboard(int navIndex = 0)
    {
        SafeInvoke(() =>
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new DashboardWindow(_monitor, AppendLog);
                _dashboardWindow.Closed += (s, e) => _dashboardWindow = null;
                if (_lastPlan != null)
                {
                    _dashboardWindow.UpdateOverviewState(_lastPlan);
                }
            }

            _dashboardWindow.SelectNav(navIndex);
            _dashboardWindow.Show();
            if (_dashboardWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _dashboardWindow.WindowState = System.Windows.WindowState.Normal;
            }
            _dashboardWindow.Activate();
        });
    }

    private void ExitApplication()
    {
        LocalizationService.LanguageChanged -= UpdateLocalization;

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
        _monitor.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        DisposeShieldIcons();
        if (_dashboardWindow != null)
        {
            _dashboardWindow.Close();
            _dashboardWindow = null;
        }

        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        catch { }

        Application.Exit();
    }

    #region Shield Icons GDI Cache & Cleanup

    private void InitShieldIcons()
    {
        _shieldIcons["green"] = CreateShieldIcon(Color.ForestGreen);
        _shieldIcons["orange"] = CreateShieldIcon(Color.DarkOrange);
        _shieldIcons["crimson"] = CreateShieldIcon(Color.Crimson);
        _shieldIcons["gray"] = CreateShieldIcon(Color.Gray);
    }

    private Icon GetShieldIcon(string key)
    {
        return _shieldIcons.TryGetValue(key, out var tuple) ? tuple.Icon : SystemIcons.Application;
    }

    private void DisposeShieldIcons()
    {
        foreach (var tuple in _shieldIcons.Values)
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
        _shieldIcons.Clear();
    }

    private static (Icon Icon, IntPtr Handle) CreateShieldIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var path = new GraphicsPath();
            path.AddLine(2, 2, 14, 2);
            path.AddLine(14, 2, 14, 8);
            path.AddBezier(14, 8, 14, 13, 8, 15, 8, 15);
            path.AddBezier(8, 15, 2, 13, 2, 8, 2, 8);
            path.CloseFigure();

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);

            using var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.0f);
            g.DrawPath(pen, path);
        }

        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        return (icon, hIcon);
    }

    #endregion
}
