using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingArmor.Common;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Services;
using PingArmor.UI.Services;

namespace PingArmor.UI.ViewModels;

/// <summary>View model for the settings page. Mirrors the original code-behind handlers.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly INetworkEngine _engine;
    private readonly NetworkMonitor _monitor;
    private readonly Action<string> _logAppender;

    /// <summary>True while values are being pushed from the config; suppresses side effects.</summary>
    public bool IsSyncing { get; private set; }

    [ObservableProperty]
    private bool _enableMetricOptimization;

    [ObservableProperty]
    private bool _enableWlanOptimizer;

    [ObservableProperty]
    private bool _disableIPv6OnWifi;

    [ObservableProperty]
    private bool _disableSmartNameResolution;

    [ObservableProperty]
    private bool _disableWpad;

    [ObservableProperty]
    private bool _flushDnsOnChange;

    [ObservableProperty]
    private bool _startupWithWindows;

    [ObservableProperty]
    private bool _restoreOnExit;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private string _theme = ThemeService.ModeSystem;

    public SettingsViewModel(
        AppConfig config,
        INetworkEngine engine,
        NetworkMonitor monitor,
        Action<string> logAppender)
    {
        _config = config;
        _engine = engine;
        _monitor = monitor;
        _logAppender = logAppender;
    }

    public string VersionText => $"PingArmor v{AppVersion.Current}";

    public bool IsThemeSystemSelected => string.Equals(Theme, ThemeService.ModeSystem, StringComparison.OrdinalIgnoreCase);
    public bool IsThemeLightSelected => string.Equals(Theme, ThemeService.ModeLight, StringComparison.OrdinalIgnoreCase);
    public bool IsThemeDarkSelected => string.Equals(Theme, ThemeService.ModeDark, StringComparison.OrdinalIgnoreCase);

    public bool IsLanguageRu => LocalizationService.CurrentLanguage == AppLanguage.Ru;
    public bool IsLanguageEn => LocalizationService.CurrentLanguage == AppLanguage.En;
    public bool IsLanguageKk => LocalizationService.CurrentLanguage == AppLanguage.Kk;

    /// <summary>Pushes the persisted configuration into the bound properties without side effects.</summary>
    public void Sync()
    {
        IsSyncing = true;
        try
        {
            EnableMetricOptimization = _config.EnableMetricOptimization;
            EnableWlanOptimizer = _config.EnableWlanOptimizer;
            DisableIPv6OnWifi = _config.DisableIPv6OnWifi;
            DisableSmartNameResolution = _config.DisableSmartNameResolution;
            DisableWpad = _config.DisableWpad;
            FlushDnsOnChange = _config.FlushDnsOnChange;
            StartupWithWindows = StartupManager.IsStartupEnabled();
            RestoreOnExit = _config.RestoreOnExit;
            ShowNotifications = _config.ShowNotifications;
            Theme = _config.Theme;
        }
        finally
        {
            IsSyncing = false;
        }

        RaiseSelectionFlags();
    }

    [RelayCommand]
    private void SetTheme(string? mode)
    {
        if (string.IsNullOrEmpty(mode)) return;
        Theme = mode;
    }

    [RelayCommand]
    private void SetLanguage(string? code)
    {
        var parsed = AppLanguageExtensions.FromCode(code);
        if (parsed == LocalizationService.CurrentLanguage) return;

        _config.Language = parsed.ToCode();
        SaveConfig();
        LocalizationService.SetLanguage(parsed);
        _logAppender($"[*] Language changed: {parsed.ToCode()}");
    }

    protected override void OnLanguageChangedCore() => RaiseSelectionFlags();

    private void RaiseSelectionFlags()
    {
        OnPropertyChanged(nameof(IsThemeSystemSelected));
        OnPropertyChanged(nameof(IsThemeLightSelected));
        OnPropertyChanged(nameof(IsThemeDarkSelected));
        OnPropertyChanged(nameof(IsLanguageRu));
        OnPropertyChanged(nameof(IsLanguageEn));
        OnPropertyChanged(nameof(IsLanguageKk));
    }

    private void SaveConfig()
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

    partial void OnEnableMetricOptimizationChanged(bool value)
    {
        if (IsSyncing) return;
        _config.EnableMetricOptimization = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'EnableMetricOptimization': ENABLED (automatic adapter priority routing active)"
            : "[*] Parameter 'EnableMetricOptimization': DISABLED (interface priority routing stopped)");
        _monitor.TriggerManualCheck();
    }

    partial void OnEnableWlanOptimizerChanged(bool value)
    {
        if (IsSyncing) return;
        _config.EnableWlanOptimizer = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'EnableWlanOptimizer': ENABLED (Wi-Fi background scan suppression active)"
            : "[*] Parameter 'EnableWlanOptimizer': DISABLED (checkbox unchecked)");

        var result = WlanOptimizerService.SetGamingMode(value);
        foreach (var line in result.Logs) _logAppender(line);

        _monitor.TriggerManualCheck();
    }

    partial void OnDisableIPv6OnWifiChanged(bool value)
    {
        if (IsSyncing) return;
        _config.DisableIPv6OnWifi = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'DisableIPv6OnWifi': ENABLED (disabling IPv6 on Wi-Fi adapters)"
            : "[*] Parameter 'DisableIPv6OnWifi': DISABLED (restoring IPv6 on Wi-Fi adapters)");

        var logs = _engine.SetIPv6OnWifiAdapters(value);
        foreach (var line in logs) _logAppender(line);
    }

    partial void OnDisableSmartNameResolutionChanged(bool value)
    {
        if (IsSyncing) return;
        _config.DisableSmartNameResolution = value;
        SaveConfig();

        var status = DnsHelper.ConfigureSmartDnsPolicy(value);
        _logAppender(value
            ? "[+] Parameter 'DisableSmartNameResolution': ENABLED (optimization active)"
            : "[*] Parameter 'DisableSmartNameResolution': DISABLED (checkbox unchecked)");
        _logAppender(value
            ? $"[+] System registry verified: {status}"
            : $"[*] System registry verified: {status}");
    }

    partial void OnDisableWpadChanged(bool value)
    {
        if (IsSyncing) return;
        _config.DisableWpad = value;
        SaveConfig();

        var status = DnsHelper.ConfigureWpadPolicy(value);
        _logAppender(value
            ? "[+] Parameter 'DisableWpad' (WPAD): ENABLED (optimization active)"
            : "[*] Parameter 'DisableWpad' (WPAD): DISABLED (checkbox unchecked)");
        _logAppender(value
            ? $"[+] System registry verified: {status}"
            : $"[*] System registry verified: {status}");
    }

    partial void OnFlushDnsOnChangeChanged(bool value)
    {
        if (IsSyncing) return;
        _config.FlushDnsOnChange = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'FlushDnsOnChange': ENABLED (automatic DNS cache flushing on network change)"
            : "[*] Parameter 'FlushDnsOnChange': DISABLED (checkbox unchecked)");

        if (value)
        {
            bool flushed = DnsHelper.FlushDnsCache();
            _logAppender(flushed
                ? "[+] System DNS cache verified: successfully flushed (DnsFlushResolverCache)"
                : "[-] Failed to flush system DNS cache");
        }
    }

    partial void OnStartupWithWindowsChanged(bool value)
    {
        if (IsSyncing) return;
        if (value) StartupManager.EnableStartup();
        else StartupManager.DisableStartup();

        bool enabled = StartupManager.IsStartupEnabled();
        if (enabled != value)
        {
            IsSyncing = true;
            StartupWithWindows = enabled;
            IsSyncing = false;
        }

        _logAppender(enabled
            ? "[+] Parameter 'Startup': ENABLED"
            : "[*] Parameter 'Startup': DISABLED (checkbox unchecked)");
        _logAppender(enabled
            ? "[+] System task verified: Windows Task Scheduler -> 'PingArmor' task active (launch on logon with highest privileges)"
            : "[*] System task verified: Windows Task Scheduler -> 'PingArmor' task removed");
    }

    partial void OnRestoreOnExitChanged(bool value)
    {
        if (IsSyncing) return;
        _config.RestoreOnExit = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'RestoreOnExit': ENABLED (system settings will revert when PingArmor exits)"
            : "[*] Parameter 'RestoreOnExit': DISABLED (changes will persist when PingArmor exits)");
    }

    partial void OnShowNotificationsChanged(bool value)
    {
        if (IsSyncing) return;
        _config.ShowNotifications = value;
        SaveConfig();
        _logAppender(value
            ? "[+] Parameter 'ShowNotifications': ENABLED (system notifications active)"
            : "[*] Parameter 'ShowNotifications': DISABLED (system notifications muted)");
    }

    partial void OnThemeChanged(string value)
    {
        if (IsSyncing) return;
        _config.Theme = value;
        SaveConfig();
        ThemeService.Apply(value);
        _logAppender($"[*] Theme changed: {value}");
        OnPropertyChanged(nameof(IsThemeSystemSelected));
        OnPropertyChanged(nameof(IsThemeLightSelected));
        OnPropertyChanged(nameof(IsThemeDarkSelected));
    }
}