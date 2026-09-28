using System;
using System.ComponentModel;

namespace PingArmor.Localization;

/// <summary>
/// Application localization.
/// <see cref="Current"/> implements <see cref="INotifyPropertyChanged"/> so XAML views
/// refresh automatically when the language changes; static accessors are kept for CLI,
/// tray and legacy code-behind.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Current { get; } = new();

    public static event Action? LanguageChanged;

    private AppLanguage _language = AppLanguage.Ru;
    private LocalizedStrings _texts = LocalizedStrings.Ru;

    private LocalizationService() { }

    /// <summary>Bindable texts for XAML, e.g. <c>Text="{Binding Texts.NavOverview}"</c>.</summary>
    public LocalizedStrings Texts => _texts;

    public AppLanguage Language => _language;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static AppLanguage CurrentLanguage => Current.Language;

    public static LocalizedStrings Strings => Current.Texts;

    public static void SetLanguage(AppLanguage language) => Current.SetLanguageInternal(language);

    public static void SetLanguage(string? code)
    {
        var lang = AppLanguageExtensions.FromCode(code);
        SetLanguage(lang);
    }

    private void SetLanguageInternal(AppLanguage language)
    {
        if (_language == language) return;

        _language = language;
        _texts = language switch
        {
            AppLanguage.En => LocalizedStrings.En,
            AppLanguage.Kk => LocalizedStrings.Kk,
            _ => LocalizedStrings.Ru
        };

        // An empty property name invalidates every binding whose source is this instance.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        LanguageChanged?.Invoke();
    }
}