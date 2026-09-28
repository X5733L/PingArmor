using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;
using PingArmor.UI.Services;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace PingArmor.UI;

public partial class DashboardWindow : FluentWindow
{
    private readonly NetworkMonitor _monitor;
    private readonly Action<string> _logAppender;

    public DashboardWindow(NetworkMonitor monitor, Action<string> logAppender)
    {
        _monitor = monitor;
        _logAppender = logAppender;

        InitializeComponent();
        Loaded += (_, _) => ThemeService.RegisterWindow(this);
        DataContext = LocalizationService.Current;

        LocalizationService.LanguageChanged += OnLanguageChanged;
        LogService.LogAppended += OnLogAppended;

        var recent = LogService.GetRecentLogs();
        if (recent.Count > 0)
        {
            TbLog.Text = string.Join(Environment.NewLine, recent) + Environment.NewLine;
            TbLog.ScrollToEnd();
        }

        UpdateRollbackCard();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPageByTag(tag);
        }
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPageByTag(tag);
        }
    }

    public void SelectNav(int index)
    {
        switch (index)
        {
            case 0:
                ShowPageByTag("Overview");
                break;
            case 1:
                ShowPageByTag("Adapters");
                AdaptersPage.Refresh();
                break;
            case 2:
                ShowPageByTag("Settings");
                SettingsPage.Sync();
                break;
            case 3:
                ShowPageByTag("Rollback");
                UpdateRollbackCard();
                break;
            case 4:
                ShowPageByTag("Log");
                break;
        }
    }

    private void ShowPageByTag(string tag)
    {
        PageOverview.Visibility = (tag == "Overview") ? Visibility.Visible : Visibility.Collapsed;
        PageAdapters.Visibility = (tag == "Adapters") ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = (tag == "Settings") ? Visibility.Visible : Visibility.Collapsed;
        PageRollback.Visibility = (tag == "Rollback") ? Visibility.Visible : Visibility.Collapsed;
        PageLog.Visibility = (tag == "Log") ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "Adapters") AdaptersPage.Refresh();
        if (tag == "Rollback") UpdateRollbackCard();
        if (tag == "Settings") SettingsPage.Sync();
    }

    #region Overview Page

    /// <summary>Forwards the latest plan to the overview view (also called by the tray).</summary>
    public void UpdateOverviewState(OptimizationPlan? plan = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => UpdateOverviewState(plan));
            return;
        }

        OverviewPage.Update(plan);
    }

    private void BtnOptimizeNow_Click(object sender, RoutedEventArgs e)
    {
        _monitor.TriggerManualCheck();
    }

    #endregion
    #region Adapters Page

    // Adapters are handled by AdaptersView / AdaptersViewModel (see UI/Views, UI/ViewModels).

    #endregion

    #region Rollback & Reset Page

    private void UpdateRollbackCard()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(UpdateRollbackCard);
            return;
        }

        var info = BackupService.GetBackupInfo();
        var s = LocalizationService.Strings;

        if (info.Exists && info.CreatedAt.HasValue)
        {
            TxtBackupStatus.Text = string.Format(s.RollbackBackupStatusFound, info.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"), info.AdapterCount);
            TxtBackupStatus.Foreground = ThemeBrush("StatusOkBrush");
        }
        else
        {
            TxtBackupStatus.Text = s.RollbackBackupStatusNotFound;
            TxtBackupStatus.Foreground = ThemeBrush("StatusWarnBrush");
        }
    }

    private void BtnRestoreSettings_Click(object sender, RoutedEventArgs e)
    {
        var s = LocalizationService.Strings;
        if (!BackupService.BackupExists())
        {
            System.Windows.MessageBox.Show(s.RestoreNoBackup, s.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = System.Windows.MessageBox.Show(s.RestoreConfirmMessage, s.AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        var logs = BackupService.RestoreFromBackup();
        foreach (var l in logs) _logAppender(l);
        WlanOptimizerService.RestoreDefaultScan();

        UpdateRollbackCard();
        UpdateOverviewState();
        System.Windows.MessageBox.Show(s.RestoreComplete, s.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnOpenWindowsSettings_Click(object sender, RoutedEventArgs e)
    {
        BackupService.OpenWindowsNetworkResetSettings();
    }

    #endregion

    #region Log Page

    public void AppendLogText(string text)
    {
        if (TbLog == null) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AppendLogText(text));
            return;
        }
        TbLog.AppendText(text + Environment.NewLine);
        TbLog.ScrollToEnd();
    }

    private void OnLogAppended(string line)
    {
        AppendLogText(line);
    }

    private void BtnLogClear_Click(object sender, RoutedEventArgs e)
    {
        TbLog.Clear();
    }

    private void BtnLogOpenFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = LogService.LogFilePath;
            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to open log file: {ex.Message}");
        }
    }

    private void BtnLogCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TbLog.Text))
        {
            System.Windows.Clipboard.SetText(TbLog.Text);
            _logAppender(LocalizationService.Strings.LogCopiedToast);
        }
    }

    #endregion

    #region Localization & Lifetime

    private void OnLanguageChanged()
    {
        UpdateOverviewState();
        UpdateRollbackCard();
    }

    private static System.Windows.Media.Brush ThemeBrush(string key)
        => System.Windows.Application.Current?.TryFindResource(key) as System.Windows.Media.Brush
           ?? System.Windows.Media.Brushes.Gray;

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't kill application on X close; hide to background tray
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        LogService.LogAppended -= OnLogAppended;
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        base.OnClosed(e);
    }

    #endregion
}

