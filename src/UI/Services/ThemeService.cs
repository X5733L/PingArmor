using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PingArmor.UI.Services;

/// <summary>
/// Applies the application theme (System / Light / Dark) through WPF-UI and keeps
/// PingArmor semantic design tokens (see UI/Themes/Tokens.*.xaml) in sync.
/// </summary>
public static class ThemeService
{
    public const string ModeSystem = "System";
    public const string ModeLight = "Light";
    public const string ModeDark = "Dark";

    private const string LightTokensUri = "pack://application:,,,/PingArmor;component/UI/Themes/Tokens.Light.xaml";
    private const string DarkTokensUri = "pack://application:,,,/PingArmor;component/UI/Themes/Tokens.Dark.xaml";

    private static bool _initialized;
    private static bool _isSystemMode = true;
    private static bool _isWatchingSystem;
    private static Window? _window;

    /// <summary>Subscribes to theme changes and applies the requested mode. Safe to call once at startup.</summary>
    public static void Initialize(string? mode)
    {
        if (!_initialized)
        {
            ApplicationThemeManager.Changed += (theme, _) => ApplyTokens(theme);
            _initialized = true;
        }

        Apply(mode);
    }

    /// <summary>Registers the main window so the "System" mode can follow OS theme changes.</summary>
    public static void RegisterWindow(Window window)
    {
        _window = window;
        UpdateSystemWatcher();
    }

    /// <summary>Detaches a window that is being closed so the OS-theme watcher no longer references it.</summary>
    public static void UnregisterWindow(Window window)
    {
        if (!ReferenceEquals(_window, window)) return;

        if (_isWatchingSystem)
        {
            try
            {
                if (window.IsLoaded)
                {
                    SystemThemeWatcher.UnWatch(window);
                }
            }
            catch (InvalidOperationException)
            {
                // Window handle is already gone.
            }

            _isWatchingSystem = false;
        }

        _window = null;
    }

    /// <summary>Applies the requested theme mode: "System", "Light" or "Dark" (case-insensitive).</summary>
    public static void Apply(string? mode)
    {
        _isSystemMode =
            !string.Equals(mode, ModeLight, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, ModeDark, StringComparison.OrdinalIgnoreCase);

        if (_isSystemMode)
        {
            ApplicationThemeManager.ApplySystemTheme();
        }
        else if (string.Equals(mode, ModeLight, StringComparison.OrdinalIgnoreCase))
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
        }
        else
        {
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
        }

        // Apply() is a no-op when the theme dictionary is already correct and raises no event,
        // so the tokens are synced explicitly here as well.
        ApplyTokens(ApplicationThemeManager.GetAppTheme());
        UpdateSystemWatcher();
    }

    private static void ApplyTokens(ApplicationTheme theme)
    {
        bool dark = theme is ApplicationTheme.Dark or ApplicationTheme.HighContrast;

        var app = System.Windows.Application.Current;
        if (app is null) return;

        var dictionaries = app.Resources.MergedDictionaries;

        ResourceDictionary? existing = null;
        foreach (var dictionary in dictionaries)
        {
            string? source = dictionary.Source?.ToString();
            if (source is null) continue;

            if (source.Contains("Tokens.Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("Tokens.Dark.xaml", StringComparison.OrdinalIgnoreCase))
            {
                existing = dictionary;
                break;
            }
        }

        var replacement = new ResourceDictionary
        {
            Source = new Uri(dark ? DarkTokensUri : LightTokensUri, UriKind.Absolute)
        };

        if (existing is not null)
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }
        else
        {
            dictionaries.Add(replacement);
        }
    }

    private static void UpdateSystemWatcher()
    {
        // The watcher requires a loaded window handle; mode changes happen from the dashboard,
        // which is loaded by the time the user can toggle the theme.
        if (_window is null || !_window.IsLoaded) return;

        if (_isSystemMode)
        {
            if (!_isWatchingSystem)
            {
                SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica);
                _isWatchingSystem = true;
            }
        }
        else if (_isWatchingSystem)
        {
            try
            {
                SystemThemeWatcher.UnWatch(_window);
            }
            catch (InvalidOperationException)
            {
                // Window handle is not available yet; nothing to unwatch.
            }

            _isWatchingSystem = false;
        }
    }
}