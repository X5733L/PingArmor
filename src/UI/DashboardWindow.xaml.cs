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
            BtnOverviewToggle.Content = CleanEmoji(s.ResumeProtection);
            IconOverviewToggle.Symbol = SymbolRegular.Play24;
            TxtHeroStatus.Text = s.StatusPaused;
            TxtHeroStatus.Foreground = System.Windows.Media.Brushes.Gray;
            TxtHeroDesc.Text = "Мониторинг сети приостановлен пользователем.";
        }
        else
        {
            BtnOverviewToggle.Content = CleanEmoji(s.PauseProtection);
            IconOverviewToggle.Symbol = SymbolRegular.Pause24;
            if (plan != null)
            {
                if (plan.NeedsOptimization)
                {
                    TxtHeroStatus.Text = s.StatusAdjusting;
                    TxtHeroStatus.Foreground = System.Windows.Media.Brushes.Goldenrod;
                }
                else
                {
                    TxtHeroStatus.Text = s.StatusProtected;
                    TxtHeroStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                }
                TxtHeroDesc.Text = plan.Summary;

                if (plan.PrimaryAdapter != null)
                {
                    TxtPrimaryAdapterName.Text = plan.PrimaryAdapter.Name;
                    TxtPrimaryAdapterDetails.Text = $"Тип: {plan.PrimaryAdapter.Type} • Метрика IPv4: {plan.PrimaryAdapter.CurrentIPv4Metric} • Интернет: {(plan.PrimaryAdapter.HasInternet ? "Есть" : "Нет")}";
                }
                else
                {
                    TxtPrimaryAdapterName.Text = s.PrimaryChannelNone;
                    TxtPrimaryAdapterDetails.Text = s.TrayTextNoConnection;
                }
            }
        }

        TxtWlanGamingStatus.Text = _config.EnableWlanOptimizer
            ? "📶 Фоновый поиск Wi-Fi: Отключен (Игровой режим включен, лаг-спайки устранены)"
            : "📶 Фоновый поиск Wi-Fi: Стандартный режим Windows";
        TxtWlanGamingStatus.Foreground = _config.EnableWlanOptimizer
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129))
            : System.Windows.Media.Brushes.Gray;
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
        var excludedSet = new HashSet<string>(_config.ExcludeAdapters ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
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

        _config.ExcludeAdapters ??= new List<string>();
        if (isExcluded)
        {
            if (!_config.ExcludeAdapters.Contains(adapterName, StringComparer.OrdinalIgnoreCase))
            {
                _config.ExcludeAdapters.Add(adapterName);
                _logAppender($"[*] Adapter '{adapterName}' added to exclusions.");
            }
        }
        else
        {
            _config.ExcludeAdapters.RemoveAll(x => x.Equals(adapterName, StringComparison.OrdinalIgnoreCase));
            _logAppender($"[*] Adapter '{adapterName}' removed from exclusions.");
        }
        _config.Save();
        _monitor.TriggerManualCheck();
    }

    #endregion

    #region Settings Page

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
        _config.Save();
        var res = WlanOptimizerService.SetGamingMode(val);
        foreach (var l in res.Logs) _logAppender(l);
        UpdateOverviewState();
    }

    private void SwMetricOpt_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.EnableMetricOptimization = SwMetricOpt.IsChecked ?? false;
        _config.Save();
        _monitor.TriggerManualCheck();
    }

    private void SwStartup_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        bool val = SwStartup.IsChecked ?? false;
        if (val) StartupManager.EnableStartup();
        else StartupManager.DisableStartup();
        SwStartup.IsChecked = StartupManager.IsStartupEnabled();
    }

    private void SwRestoreOnExit_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.RestoreOnExit = SwRestoreOnExit.IsChecked ?? false;
        _config.Save();
    }

    private void SwNotifications_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.ShowNotifications = SwNotifications.IsChecked ?? false;
        _config.Save();
    }

    private void SwDisableIPv6_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.DisableIPv6OnWifi = SwDisableIPv6.IsChecked ?? false;
        _config.Save();
        _monitor.TriggerManualCheck();
    }

    private void SwDisableSmartDns_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.DisableSmartNameResolution = SwDisableSmartDns.IsChecked ?? false;
        _config.Save();
        _monitor.TriggerManualCheck();
    }

    private void SwDisableWpad_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.DisableWpad = SwDisableWpad.IsChecked ?? false;
        _config.Save();
        _monitor.TriggerManualCheck();
    }

    private void SwFlushDns_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSwitches) return;
        _config.FlushDnsOnChange = SwFlushDns.IsChecked ?? false;
        _config.Save();
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string langCode)
        {
            var parsed = AppLanguageExtensions.FromCode(langCode);
            _config.Language = parsed.ToCode();
            _config.Save();
            LocalizationService.SetLanguage(parsed);
        }
    }

    private void UpdateLanguageButtonHighlight()
    {
        var cur = LocalizationService.CurrentLanguage;
        HighlightLangButton(BtnLangRu, cur == AppLanguage.Ru);
        HighlightLangButton(BtnLangEn, cur == AppLanguage.En);
        HighlightLangButton(BtnLangKk, cur == AppLanguage.Kk);
    }

    private static void HighlightLangButton(Wpf.Ui.Controls.Button btn, bool active)
    {
        btn.Appearance = active ? ControlAppearance.Primary : ControlAppearance.Secondary;
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
            TxtBackupStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        }
        else
        {
            TxtBackupStatus.Text = s.RollbackBackupStatusNotFound;
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Goldenrod;
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
            _logAppender($"[-] Не удалось открыть файл лога: {ex.Message}");
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

        Title = s.DashboardTitle;
        AppTitleBar.Title = s.DashboardTitle;

        NavItemOverview.Content = s.NavOverview;
        NavItemAdapters.Content = s.NavAdapters;
        NavItemRollback.Content = s.NavRollback;
        NavItemLog.Content = s.NavLog;
        NavItemSettings.Content = s.NavTuning;

        TxtOverviewTitle.Text = s.NavOverview;
        TxtAdaptersTitle.Text = s.NavAdapters;
        TxtRollbackTitle.Text = s.NavRollback;
        TxtLogTitle.Text = s.NavLog;
        TxtSettingsTitle.Text = s.NavTuning;

        BtnOverviewOptimize.Content = CleanEmoji(s.OptimizeNow);
        BtnAdaptersOptimize.Content = CleanEmoji(s.OptimizeNow);
        BtnLogOptimize.Content = CleanEmoji(s.OptimizeNow);

        BtnAdaptersRefresh.Content = LocalizationService.CurrentLanguage switch
        {
            AppLanguage.En => "Refresh list",
            AppLanguage.Kk => "Тізімді жаңарту",
            _ => "Обновить список"
        };
        BtnLogOptimize.Content = CleanEmoji(s.OptimizeNow);
        BtnLogClear.Content = "Очистить";
        BtnLogOpenFile.Content = CleanEmoji(s.LogOpenFile);
        BtnLogCopy.Content = CleanEmoji(s.CopyLog);

        ColExclude.Header = s.AdaptersHeaderExclude;
        ColName.Header = s.AdaptersHeaderName;
        ColType.Header = s.AdaptersHeaderType;
        ColStatus.Header = s.AdaptersHeaderStatus;
        ColMetric.Header = s.AdaptersHeaderMetric;
        ColInternet.Header = s.AdaptersHeaderInternet;

        TxtSettingsGroupNetwork.Text = s.SettingsGroupNetwork;
        TxtSettingsGroupApp.Text = s.SettingsGroupApp;

        SetCardHeader(CardGamingMode, CleanEmoji(s.GamingMode), s.WlanOptimizerDesc);
        SetCardHeader(CardMetricOpt, CleanEmoji(s.MetricOptimizationTitle), s.MetricOptimizationDesc);
        SetCardHeader(CardStartup, CleanEmoji(s.StartupWithWindows), s.StartupDesc);
        SetCardHeader(CardRestoreOnExit, CleanEmoji(s.RestoreOnExit), s.RestoreOnExitDesc);
        SetCardHeader(CardNotifications, CleanEmoji(s.Notifications), s.NotificationsDesc);
        SetCardHeader(CardDisableIPv6, CleanEmoji(s.DisableIPv6OnWifiTitle), s.DisableIPv6OnWifiDesc);
        SetCardHeader(CardDisableSmartDns, CleanEmoji(s.DisableSmartDnsTitle), s.DisableSmartDnsDesc);
        SetCardHeader(CardDisableWpad, CleanEmoji(s.DisableWpadTitle), s.DisableWpadDesc);
        SetCardHeader(CardFlushDns, CleanEmoji(s.FlushDnsTitle), s.FlushDnsDesc);

        SetCardHeader(CardLang, s.LanguageInterfaceTitle, s.LanguageInterfaceDesc);

        var currentLang = LocalizationService.CurrentLanguage;
        BtnLangRu.Appearance = currentLang == AppLanguage.Ru ? ControlAppearance.Primary : ControlAppearance.Secondary;
        BtnLangEn.Appearance = currentLang == AppLanguage.En ? ControlAppearance.Primary : ControlAppearance.Secondary;
        BtnLangKk.Appearance = currentLang == AppLanguage.Kk ? ControlAppearance.Primary : ControlAppearance.Secondary;

        BtnRestoreSettingsCard.Content = CleanEmoji(s.RestoreSettings);
        BtnRollbackTopRestore.Content = CleanEmoji(s.RestoreSettings);
        TxtWindowsResetTitle.Text = s.RollbackWindowsResetTitle;
        TxtWindowsResetDesc.Text = s.RollbackWindowsResetDesc;
        BtnOpenWindowsSettings.Content = CleanEmoji(s.BtnOpenWindowsSettings);

        TxtAboutApp.Text = $"PingArmor v{AppVersion.Current}";
        TxtAboutIconCredit.Text = currentLang switch
        {
            AppLanguage.Kk => "Қолданба белгішесі: Magnific (Flaticon)",
            AppLanguage.En => "Application icon created by Magnific (Flaticon)",
            _ => "Иконка приложения: Magnific (Flaticon)"
        };
    }

    private static string CleanEmoji(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"^[\p{Cs}\p{So}\p{Sk}\s⚡📶🚀🔔🚫🌐🛡️🧹]+", "").Trim();
    }

    private static void SetCardHeader(CardControl card, string title, string description)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        sp.Children.Add(new TextBlock { Text = description, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)), FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
        card.Header = sp;
    }

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
