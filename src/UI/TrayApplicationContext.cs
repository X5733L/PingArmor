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
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _primaryAdapterMenuItem;
    private readonly ToolStripMenuItem _optimizeNowItem;
    private readonly ToolStripMenuItem _toggleMonitoringMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly ToolStripMenuItem _notificationsMenuItem;
    private readonly ToolStripMenuItem _gamingModeMenuItem;
    private readonly ToolStripMenuItem _restoreOnExitMenuItem;
    private readonly ToolStripMenuItem _languageSubmenu;
    private readonly ToolStripMenuItem _languageRuItem;
    private readonly ToolStripMenuItem _languageEnItem;
    private readonly ToolStripMenuItem _languageKkItem;
    private readonly ToolStripMenuItem _showLogsItem;
    private readonly ToolStripMenuItem _restoreSettingsItem;
    private readonly ToolStripMenuItem? _elevateItem;
    private readonly ToolStripMenuItem _exitItem;

    private Form? _logForm;
    private TextBox? _logTextBox;
    private Button? _btnOptimizeLog;
    private Button? _btnClearLog;

    private readonly Dictionary<string, (Icon Icon, IntPtr Handle)> _shieldIcons = new();
    private OptimizationPlan? _lastPlan;

    public TrayApplicationContext(AppConfig config, NetworkEngine engine, NetworkMonitor monitor)
    {
        _config = config;
        _engine = engine;
        _monitor = monitor;

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

        _statusMenuItem = new ToolStripMenuItem(strings.StatusInitializing) { Enabled = false };
        _primaryAdapterMenuItem = new ToolStripMenuItem(strings.PrimaryChannelDetecting) { Enabled = false };

        _optimizeNowItem = new ToolStripMenuItem(strings.OptimizeNow, null, (s, e) => _monitor.TriggerManualCheck());
        _toggleMonitoringMenuItem = new ToolStripMenuItem(strings.PauseProtection, null, OnToggleMonitoring);

        _startupMenuItem = new ToolStripMenuItem(strings.StartupWithWindows, null, OnToggleStartup)
        {
            CheckOnClick = true,
            Checked = StartupManager.IsStartupEnabled()
        };

        _notificationsMenuItem = new ToolStripMenuItem(strings.Notifications, null, OnToggleNotifications)
        {
            CheckOnClick = true,
            Checked = _config.ShowNotifications
        };

        _gamingModeMenuItem = new ToolStripMenuItem(strings.GamingMode, null, OnToggleGamingMode)
        {
            CheckOnClick = true,
            Checked = _config.EnableWlanOptimizer
        };

        _restoreOnExitMenuItem = new ToolStripMenuItem(strings.RestoreOnExit, null, OnToggleRestoreOnExit)
        {
            CheckOnClick = true,
            Checked = _config.RestoreOnExit
        };

        // Language selection submenu
        _languageSubmenu = new ToolStripMenuItem(strings.LanguageSubmenu);
        _languageRuItem = new ToolStripMenuItem("Русский", null, (s, e) => SwitchLanguage(AppLanguage.Ru));
        _languageEnItem = new ToolStripMenuItem("English", null, (s, e) => SwitchLanguage(AppLanguage.En));
        _languageKkItem = new ToolStripMenuItem("Қазақша", null, (s, e) => SwitchLanguage(AppLanguage.Kk));
        _languageSubmenu.DropDownItems.AddRange(new ToolStripItem[] { _languageRuItem, _languageEnItem, _languageKkItem });

        _showLogsItem = new ToolStripMenuItem(strings.EventLog, null, (s, e) => ShowLogForm());
        _restoreSettingsItem = new ToolStripMenuItem(strings.RestoreSettings, null, OnRestoreSettings);
        _exitItem = new ToolStripMenuItem(strings.Exit, null, (s, e) => ExitApplication());

        _contextMenu.Items.Add(_headerMenuItem);
        if (_elevateItem != null)
        {
            _contextMenu.Items.Add(_elevateItem);
        }
        _contextMenu.Items.Add(_statusMenuItem);
        _contextMenu.Items.Add(_primaryAdapterMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_optimizeNowItem);
        _contextMenu.Items.Add(_toggleMonitoringMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_startupMenuItem);
        _contextMenu.Items.Add(_gamingModeMenuItem);
        _contextMenu.Items.Add(_restoreOnExitMenuItem);
        _contextMenu.Items.Add(_notificationsMenuItem);
        _contextMenu.Items.Add(_languageSubmenu);
        _contextMenu.Items.Add(_showLogsItem);
        _contextMenu.Items.Add(_restoreSettingsItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = strings.AppTitle,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Icon = GetShieldIcon("green")
        };

        _notifyIcon.DoubleClick += (s, e) => ShowLogForm();

        // Subscribe to language changes for instant UI refresh
        LocalizationService.LanguageChanged += UpdateLocalization;
        UpdateLanguageMenuItems();

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

    private void SwitchLanguage(AppLanguage language)
    {
        _config.Language = language.ToCode();
        _config.Save();
        LocalizationService.SetLanguage(language);
    }

    private void UpdateLanguageMenuItems()
    {
        var current = LocalizationService.CurrentLanguage;
        _languageRuItem.Checked = current == AppLanguage.Ru;
        _languageEnItem.Checked = current == AppLanguage.En;
        _languageKkItem.Checked = current == AppLanguage.Kk;
    }

    private void UpdateLocalization()
    {
        var s = LocalizationService.Strings;
        bool isAdmin = Program.IsAdministrator();

        _headerMenuItem.Text = isAdmin ? s.HeaderAdmin : s.HeaderNoAdmin;
        if (_elevateItem != null)
        {
            _elevateItem.Text = s.RestartAsAdmin;
        }

        _optimizeNowItem.Text = s.OptimizeNow;
        _toggleMonitoringMenuItem.Text = _monitor.IsRunning ? s.PauseProtection : s.ResumeProtection;
        _startupMenuItem.Text = s.StartupWithWindows;
        _gamingModeMenuItem.Text = s.GamingMode;
        _restoreOnExitMenuItem.Text = s.RestoreOnExit;
        _notificationsMenuItem.Text = s.Notifications;
        _languageSubmenu.Text = s.LanguageSubmenu;
        _showLogsItem.Text = s.EventLog;
        _restoreSettingsItem.Text = s.RestoreSettings;
        _exitItem.Text = s.Exit;

        UpdateLanguageMenuItems();

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

        // Update opened log window if currently active
        if (_logForm != null && !_logForm.IsDisposed)
        {
            _logForm.Text = GetLogWindowTitle(s);
            if (_btnOptimizeLog != null) _btnOptimizeLog.Text = s.OptimizeNow;
            if (_btnClearLog != null) _btnClearLog.Text = s.Clear;
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
        var s = LocalizationService.Strings;
        _toggleMonitoringMenuItem.Text = isRunning ? s.PauseProtection : s.ResumeProtection;

        if (!isRunning)
        {
            _notifyIcon.Icon = GetShieldIcon("gray");
            _statusMenuItem.Text = s.StatusPaused;
        }
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        var s = LocalizationService.Strings;
        if (_startupMenuItem.Checked)
        {
            bool success = StartupManager.EnableStartup();
            _startupMenuItem.Checked = success;
            AppendLog(success ? s.StartupTaskSuccess : s.StartupTaskFail);
        }
        else
        {
            bool success = StartupManager.DisableStartup();
            _startupMenuItem.Checked = !success;
            AppendLog(success ? s.StartupTaskRemoveSuccess : s.StartupTaskRemoveFail);
        }
    }

    private void OnToggleGamingMode(object? sender, EventArgs e)
    {
        var s = LocalizationService.Strings;
        _config.EnableWlanOptimizer = _gamingModeMenuItem.Checked;
        _config.Save();

        var res = WlanOptimizerService.SetGamingMode(_config.EnableWlanOptimizer);
        foreach (var log in res.Logs)
        {
            AppendLog(log);
        }

        if (_config.ShowNotifications)
        {
            string msg = _config.EnableWlanOptimizer
                ? s.GamingModeEnabledToast
                : s.GamingModeDisabledToast;

            _notifyIcon.ShowBalloonTip(3000, s.AppTitle, msg, ToolTipIcon.Info);
        }
    }

    private void OnToggleNotifications(object? sender, EventArgs e)
    {
        var s = LocalizationService.Strings;
        _config.ShowNotifications = _notificationsMenuItem.Checked;
        _config.Save();

        string state = _config.ShowNotifications ? s.NotificationsEnabled : s.NotificationsDisabled;
        AppendLog(string.Format(s.NotificationsToggledFormat, state));
    }

    private void OnToggleRestoreOnExit(object? sender, EventArgs e)
    {
        _config.RestoreOnExit = _restoreOnExitMenuItem.Checked;
        _config.Save();
        AppendLog($"[*] Restore settings on exit: {_config.RestoreOnExit}");
    }

    private void OnRestoreSettings(object? sender, EventArgs e)
    {
        var s = LocalizationService.Strings;

        if (!BackupService.BackupExists())
        {
            AppendLog(s.RestoreNoBackup);
            if (_config.ShowNotifications)
            {
                _notifyIcon.ShowBalloonTip(3000, s.AppTitle, s.RestoreNoBackup, ToolTipIcon.Warning);
            }
            return;
        }

        var confirmResult = MessageBox.Show(
            s.RestoreConfirmMessage,
            s.AppTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmResult != DialogResult.Yes) return;

        AppendLog(s.RestoreStarting);
        var logs = BackupService.RestoreFromBackup();
        foreach (var log in logs)
        {
            AppendLog(log);
        }

        WlanOptimizerService.RestoreDefaultScan();
        AppendLog(s.RestoreComplete);

        if (_config.ShowNotifications)
        {
            _notifyIcon.ShowBalloonTip(3000, s.AppTitle, s.RestoreComplete, ToolTipIcon.Info);
        }
    }

    private void OnPlanEvaluated(OptimizationPlan plan)
    {
        _lastPlan = plan;
        RenderPlanStatus(plan);
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
        var s = LocalizationService.Strings;
        _notifyIcon.Icon = GetShieldIcon("green");
        if (_config.ShowNotifications && result.ActionsApplied > 0)
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                s.AppTitle,
                string.Format(s.BalloonOptimizedFormat, result.ActionsApplied),
                ToolTipIcon.Info
            );
        }
    }

    private void AppendLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_logTextBox != null && !_logTextBox.IsDisposed)
        {
            _logTextBox.BeginInvoke(new Action(() =>
            {
                _logTextBox.AppendText(entry + Environment.NewLine);
            }));
        }
    }

    private void ShowLogForm()
    {
        if (_logForm != null && !_logForm.IsDisposed)
        {
            _logForm.BringToFront();
            _logForm.Focus();
            return;
        }

        var s = LocalizationService.Strings;

        _logForm = new Form
        {
            Text = GetLogWindowTitle(s),
            Size = new Size(680, 480),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White
        };

        _logTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 9.5f)
        };

        var panel = new Panel { Dock = DockStyle.Bottom, Height = 45 };
        _btnOptimizeLog = new Button
        {
            Text = s.OptimizeNow,
            Location = new Point(10, 8),
            Size = new Size(180, 30),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat
        };
        _btnOptimizeLog.Click += (s, e) => _monitor.TriggerManualCheck();

        _btnClearLog = new Button
        {
            Text = s.Clear,
            Location = new Point(200, 8),
            Size = new Size(90, 30),
            BackColor = Color.FromArgb(50, 50, 50),
            FlatStyle = FlatStyle.Flat
        };
        _btnClearLog.Click += (s, e) => _logTextBox.Clear();

        panel.Controls.Add(_btnOptimizeLog);
        panel.Controls.Add(_btnClearLog);

        _logForm.Controls.Add(_logTextBox);
        _logForm.Controls.Add(panel);

        _logForm.Show();
        AppendLog(s.LogOpened);
    }

    private static string GetLogWindowTitle(LocalizedStrings strings)
    {
        if (strings.LogWindowTitle.StartsWith("PingArmor -", StringComparison.Ordinal))
        {
            return strings.LogWindowTitle.Replace("PingArmor -", $"PingArmor v{AppVersion.Current} -");
        }
        return $"PingArmor v{AppVersion.Current} - {strings.LogWindowTitle}";
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
        foreach (var pair in _shieldIcons.Values)
        {
            try
            {
                pair.Icon.Dispose();
                DestroyIcon(pair.Handle);
            }
            catch { }
        }
        _shieldIcons.Clear();
    }

    private static (Icon Icon, IntPtr Handle) CreateShieldIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(color);
            var points = new[]
            {
                new Point(16, 2),
                new Point(29, 6),
                new Point(29, 18),
                new Point(16, 30),
                new Point(3, 18),
                new Point(3, 6)
            };
            g.FillPolygon(brush, points);

            using var pen = new Pen(Color.White, 2f);
            g.DrawPolygon(pen, points);

            using var checkPen = new Pen(Color.White, 2.5f);
            g.DrawLines(checkPen, new[]
            {
                new Point(10, 16),
                new Point(14, 21),
                new Point(22, 11)
            });
        }

        IntPtr hIcon = bitmap.GetHicon();
        return (Icon.FromHandle(hIcon), hIcon);
    }

    #endregion
}
