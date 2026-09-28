using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PingArmor.Common;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Models;
using PingArmor.Services;
using PingArmor.UI.Services;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;
using TextBlock = System.Windows.Controls.TextBlock;

namespace PingArmor.UI;

public partial class DashboardWindow : FluentWindow
{
    private readonly AppConfig _config;
    private readonly NetworkEngine _engine;
    private readonly NetworkMonitor _monitor;
    private readonly Action<string> _logAppender;

    private readonly ObservableCollection<AdapterRowItem> _adapterRows = new();
    private OptimizationPlan? _lastPlan;
    private bool _isUpdatingSwitches;

    public DashboardWindow(AppConfig config, NetworkEngine engine, NetworkMonitor monitor, Action<string> logAppender)
    {
        _config = config;
        _engine = engine;
        _monitor = monitor;
        _logAppender = logAppender;

        InitializeComponent();
        Loaded += (_, _) => ThemeService.RegisterWindow(this);
        DataContext = LocalizationService.Current;

        GridAdapters.ItemsSource = _adapterRows;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        LogService.LogAppended += OnLogAppended;

        var recent = LogService.GetRecentLogs();
        if (recent.Count > 0)
        {
            TbLog.Text = string.Join(Environment.NewLine, recent) + Environment.NewLine;
            TbLog.ScrollToEnd();
        }

        UpdateLocalization();
        SyncTuningSwitches();
        RefreshAdaptersList();
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
                RefreshAdaptersList();
                break;
            case 2:
                ShowPageByTag("Settings");
                SyncTuningSwitches();
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

        if (tag == "Adapters") RefreshAdaptersList();
        if (tag == "Rollback") UpdateRollbackCard();
        if (tag == "Settings") SyncTuningSwitches();
    }

    #region Overview Page

    public void UpdateOverviewState(OptimizationPlan? plan = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => UpdateOverviewState(plan));
            return;
        }

        if (plan != null) _lastPlan = plan;
        plan ??= _lastPlan;

        var s = LocalizationService.Strings;
        if (!_monitor.IsRunning)
        {
            BtnOverviewToggle.Content = s.ResumeProtection;
            IconOverviewToggle.Symbol = SymbolRegular.Play24;
            TxtHeroStatus.Text = s.StatusPaused;
            TxtHeroStatus.Foreground = ThemeBrush("StatusPausedBrush");
            TxtHeroDesc.Text = s.OverviewPausedDesc;
        }
        else
        {
            BtnOverviewToggle.Content = s.PauseProtection;
            IconOverviewToggle.Symbol = SymbolRegular.Pause24;
            if (plan != null)
            {
                if (plan.NeedsOptimization)
                {
                    TxtHeroStatus.Text = s.StatusAdjusting;
                    TxtHeroStatus.Foreground = ThemeBrush("StatusWarnBrush");
                }
                else
                {
                    TxtHeroStatus.Text = s.StatusProtected;
                    TxtHeroStatus.Foreground = ThemeBrush("StatusOkBrush");
                }
                TxtHeroDesc.Text = plan.Summary;

                if (plan.PrimaryAdapter != null)
                {
                    TxtPrimaryAdapterName.Text = plan.PrimaryAdapter.Name;
                    TxtPrimaryAdapterDetails.Text = string.Format(
                        s.PrimaryAdapterDetailsFormat,
                        plan.PrimaryAdapter.Type,
                        plan.PrimaryAdapter.CurrentIPv4Metric,
                        plan.PrimaryAdapter.HasInternet ? s.InternetYes : s.InternetNo);
                }
                else
                {
                    TxtPrimaryAdapterName.Text = s.PrimaryChannelNone;
                    TxtPrimaryAdapterDetails.Text = s.TrayTextNoConnection;
                }
            }
        }

        TxtWlanGamingStatus.Text = _config.EnableWlanOptimizer
            ? s.WlanOptimizerActive
            : s.WlanOptimizerStandard;
        TxtWlanGamingStatus.Foreground = _config.EnableWlanOptimizer
            ? ThemeBrush("StatusOkBrush")
            : ThemeBrush("StatusPausedBrush");
    }

    private void BtnOptimizeNow_Click(object sender, RoutedEventArgs e)
    {
        _monitor.TriggerManualCheck();
    }

    private void BtnToggleProtection_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor.IsRunning) _monitor.Stop();
        else _monitor.Start();
        UpdateOverviewState();
    }

    #endregion

    #region Adapters Page

    public void RefreshAdaptersList()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(RefreshAdaptersList);
            return;
        }

        _adapterRows.Clear();
        var adapters = _engine.GetAdapters();
        var excludedSet = new HashSet<string>(_config.GetExcludeSnapshot(), StringComparer.OrdinalIgnoreCase);
        var s = LocalizationService.Strings;

        foreach (var a in adapters)
        {
            bool isExcluded = excludedSet.Contains(a.Name) || excludedSet.Contains(a.Description);
            string statusStr = a.IsUp ? s.AdaptersActiveTag : s.AdaptersDisconnectedTag;
            string internetStr = a.HasInternet ? "✓" : "-";

            var row = new AdapterRowItem(a.Name, a.Type.ToString(), statusStr, a.CurrentIPv4Metric.ToString(), internetStr, isExcluded);
            row.PropertyChanged += (snd, args) =>
            {
                if (args.PropertyName == nameof(AdapterRowItem.IsExcluded))
                {
                    OnAdapterExclusionToggled(row.Name, row.IsExcluded);
                }
            };
            _adapterRows.Add(row);
        }
    }

    private void BtnRefreshAdapters_Click(object sender, RoutedEventArgs e)
    {
        RefreshAdaptersList();
    }

    private void OnAdapterExclusionToggled(string adapterName, bool isExcluded)
    {
        if (string.IsNullOrEmpty(adapterName)) return;

        if (isExcluded)
        {
            if (_config.AddExclusion(adapterName))
            {
                _logAppender($"[*] Adapter '{adapterName}' added to exclusions.");
            }
        }
        else
        {
            _config.RemoveExclusion(adapterName);
            _logAppender($"[*] Adapter '{adapterName}' removed from exclusions.");
        }
        TrySaveConfig();
        _monitor.TriggerManualCheck();
    }

    #endregion

    #region Settings Page

    private void TrySaveConfig()
    {
        try
        {
            _config.Save();
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to save configuration: {ex.Message}");
        }
    }

    public void SyncTuningSwitches()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(SyncTuningSwitches);
            return;
        }

        _isUpdatingSwitches = true;
        try
        {
            SwGamingMode.IsChecked = _config.EnableWlanOptimizer;
            SwMetricOpt.IsChecked = _config.EnableMetricOptimization;
            SwStartup.IsChecked = StartupManager.IsStartupEnabled();
            SwRestoreOnExit.IsChecked = _config.RestoreOnExit;
            SwNotifications.IsChecked = _config.ShowNotifications;
            SwDisableIPv6.IsChecked = _config.DisableIPv6OnWifi;
            SwDisableSmartDns.IsChecked = _config.DisableSmartNameResolution;
            SwDisableWpad.IsChecked = _config.DisableWpad;
            SwFlushDns.IsChecked = _config.FlushDnsOnChange;

            UpdateLanguageButtonHighlight();
            UpdateThemeButtonHighlight();
        }
        finally
        {
            _isUpdatingSwitches = false;
        }
    }

    private void SwGamingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwGamingMode.IsChecked ?? false;
        _config.EnableWlanOptimizer = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'EnableWlanOptimizer': ENABLED (Wi-Fi background scan suppression active)"
            : "[*] Parameter 'EnableWlanOptimizer': DISABLED (checkbox unchecked)");
        var res = WlanOptimizerService.SetGamingMode(val);
        foreach (var l in res.Logs) _logAppender(l);
        UpdateOverviewState();
    }

    private void SwMetricOpt_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwMetricOpt.IsChecked ?? false;
        _config.EnableMetricOptimization = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'EnableMetricOptimization': ENABLED (automatic adapter priority routing active)"
            : "[*] Parameter 'EnableMetricOptimization': DISABLED (interface priority routing stopped)");
        _monitor.TriggerManualCheck();
    }

    private void SwStartup_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwStartup.IsChecked ?? false;
        if (val) StartupManager.EnableStartup();
        else StartupManager.DisableStartup();
        bool enabled = StartupManager.IsStartupEnabled();
        SwStartup.IsChecked = enabled;
        _logAppender(enabled
            ? "[+] Parameter 'Startup': ENABLED"
            : "[*] Parameter 'Startup': DISABLED (checkbox unchecked)");
        _logAppender(enabled
            ? "[+] System task verified: Windows Task Scheduler -> 'PingArmor' task active (launch on logon with highest privileges)"
            : "[*] System task verified: Windows Task Scheduler -> 'PingArmor' task removed");
    }

    private void SwRestoreOnExit_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwRestoreOnExit.IsChecked ?? false;
        _config.RestoreOnExit = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'RestoreOnExit': ENABLED (system settings will revert when PingArmor exits)"
            : "[*] Parameter 'RestoreOnExit': DISABLED (changes will persist when PingArmor exits)");
    }

    private void SwNotifications_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwNotifications.IsChecked ?? false;
        _config.ShowNotifications = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'ShowNotifications': ENABLED (system notifications active)"
            : "[*] Parameter 'ShowNotifications': DISABLED (system notifications muted)");
    }

    private void SwDisableIPv6_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwDisableIPv6.IsChecked ?? false;
        _config.DisableIPv6OnWifi = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'DisableIPv6OnWifi': ENABLED (disabling IPv6 on Wi-Fi adapters)"
            : "[*] Parameter 'DisableIPv6OnWifi': DISABLED (restoring IPv6 on Wi-Fi adapters)");
        var logs = _engine.SetIPv6OnWifiAdapters(val);
        foreach (var l in logs) _logAppender(l);
    }

    private void SwDisableSmartDns_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwDisableSmartDns.IsChecked ?? false;
        _config.DisableSmartNameResolution = val;
        TrySaveConfig();

        var status = DnsHelper.ConfigureSmartDnsPolicy(val);
        _logAppender(val
            ? "[+] Parameter 'DisableSmartNameResolution': ENABLED (optimization active)"
            : "[*] Parameter 'DisableSmartNameResolution': DISABLED (checkbox unchecked)");
        _logAppender(val
            ? $"[+] System registry verified: {status}"
            : $"[*] System registry verified: {status}");
    }

    private void SwDisableWpad_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwDisableWpad.IsChecked ?? false;
        _config.DisableWpad = val;
        TrySaveConfig();

        var status = DnsHelper.ConfigureWpadPolicy(val);
        _logAppender(val
            ? "[+] Parameter 'DisableWpad' (WPAD): ENABLED (optimization active)"
            : "[*] Parameter 'DisableWpad' (WPAD): DISABLED (checkbox unchecked)");
        _logAppender(val
            ? $"[+] System registry verified: {status}"
            : $"[*] System registry verified: {status}");
    }

    private void SwFlushDns_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwFlushDns.IsChecked ?? false;
        _config.FlushDnsOnChange = val;
        TrySaveConfig();
        _logAppender(val
            ? "[+] Parameter 'FlushDnsOnChange': ENABLED (automatic DNS cache flushing on network change)"
            : "[*] Parameter 'FlushDnsOnChange': DISABLED (checkbox unchecked)");
        if (val)
        {
            bool flushed = DnsHelper.FlushDnsCache();
            _logAppender(flushed
                ? "[+] System DNS cache verified: successfully flushed (DnsFlushResolverCache)"
                : "[-] Failed to flush system DNS cache");
        }
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string langCode)
        {
            var parsed = AppLanguageExtensions.FromCode(langCode);
            _config.Language = parsed.ToCode();
            TrySaveConfig();
            LocalizationService.SetLanguage(parsed);
        }
    }

    private void UpdateLanguageButtonHighlight()
    {
        var cur = LocalizationService.CurrentLanguage;
        HighlightChoiceButton(BtnLangRu, cur == AppLanguage.Ru);
        HighlightChoiceButton(BtnLangEn, cur == AppLanguage.En);
        HighlightChoiceButton(BtnLangKk, cur == AppLanguage.Kk);
    }

    private static void HighlightChoiceButton(Wpf.Ui.Controls.Button btn, bool active)
    {
        btn.Appearance = active ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string mode)
        {
            _config.Theme = mode;
            TrySaveConfig();
            ThemeService.Apply(mode);
            UpdateThemeButtonHighlight();
            _logAppender($"[*] Theme changed: {mode}");
        }
    }

    private void UpdateThemeButtonHighlight()
    {
        var mode = _config.Theme;
        HighlightChoiceButton(BtnThemeSystem, string.Equals(mode, ThemeService.ModeSystem, StringComparison.OrdinalIgnoreCase));
        HighlightChoiceButton(BtnThemeLight, string.Equals(mode, ThemeService.ModeLight, StringComparison.OrdinalIgnoreCase));
        HighlightChoiceButton(BtnThemeDark, string.Equals(mode, ThemeService.ModeDark, StringComparison.OrdinalIgnoreCase));
    }

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
        UpdateLocalization();
        UpdateOverviewState();
        UpdateRollbackCard();
        RefreshAdaptersList();
        SyncTuningSwitches();
    }

    private void UpdateLocalization()
    {
        var s = LocalizationService.Strings;

        ColExclude.Header = s.AdaptersHeaderExclude;
        ColName.Header = s.AdaptersHeaderName;
        ColType.Header = s.AdaptersHeaderType;
        ColStatus.Header = s.AdaptersHeaderStatus;
        ColMetric.Header = s.AdaptersHeaderMetric;
        ColInternet.Header = s.AdaptersHeaderInternet;

        SetCardHeader(CardGamingMode, s.GamingMode, s.WlanOptimizerDesc);
        SetCardHeader(CardMetricOpt, s.MetricOptimizationTitle, s.MetricOptimizationDesc);
        SetCardHeader(CardStartup, s.StartupWithWindows, s.StartupDesc);
        SetCardHeader(CardRestoreOnExit, s.RestoreOnExit, s.RestoreOnExitDesc);
        SetCardHeader(CardNotifications, s.Notifications, s.NotificationsDesc);
        SetCardHeader(CardDisableIPv6, s.DisableIPv6OnWifiTitle, s.DisableIPv6OnWifiDesc);
        SetCardHeader(CardDisableSmartDns, s.DisableSmartDnsTitle, s.DisableSmartDnsDesc);
        SetCardHeader(CardDisableWpad, s.DisableWpadTitle, s.DisableWpadDesc);
        SetCardHeader(CardFlushDns, s.FlushDnsTitle, s.FlushDnsDesc);
        SetCardHeader(CardLang, s.LanguageInterfaceTitle, s.LanguageInterfaceDesc);
        SetCardHeader(CardTheme, s.ThemeTitle, s.ThemeDesc);

        TxtAboutApp.Text = $"PingArmor v{AppVersion.Current}";

        UpdateLanguageButtonHighlight();
        UpdateThemeButtonHighlight();
    }

    private static void SetCardHeader(CardControl card, string title, string description)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        sp.Children.Add(new TextBlock { Text = description, Foreground = ThemeBrush("TextFillColorSecondaryBrush"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        card.Header = sp;
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

public class AdapterRowItem : INotifyPropertyChanged
{
    public string Name { get; }
    public string Type { get; }
    public string Status { get; }
    public string Metric { get; }
    public string Internet { get; }

    private bool _isExcluded;
    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (_isExcluded != value)
            {
                _isExcluded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExcluded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AdapterRowItem(string name, string type, string status, string metric, string internet, bool isExcluded)
    {
        Name = name;
        Type = type;
        Status = status;
        Metric = metric;
        Internet = internet;
        _isExcluded = isExcluded;
    }
}
