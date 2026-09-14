using System;

namespace PingArmor.Localization;

public static class LocalizationService
{
    private static AppLanguage _currentLanguage = AppLanguage.Ru;
    private static LocalizedStrings _strings = LocalizedStrings.Ru;

    public static event Action? LanguageChanged;

    public static AppLanguage CurrentLanguage => _currentLanguage;

    public static LocalizedStrings Strings => _strings;

    public static void SetLanguage(AppLanguage language)
    {
        if (_currentLanguage == language) return;

        _currentLanguage = language;
        _strings = language switch
        {
            AppLanguage.En => LocalizedStrings.En,
            AppLanguage.Kk => LocalizedStrings.Kk,
            _ => LocalizedStrings.Ru
        };

        LanguageChanged?.Invoke();
    }

    public static void SetLanguage(string? code)
    {
        var lang = AppLanguageExtensions.FromCode(code);
        SetLanguage(lang);
    }
}
