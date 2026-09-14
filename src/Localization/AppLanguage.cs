using System;

namespace PingArmor.Localization;

public enum AppLanguage
{
    Ru,
    En,
    Kk
}

public static class AppLanguageExtensions
{
    public const string CodeRu = "ru";
    public const string CodeEn = "en";
    public const string CodeKk = "kk";

    public static string ToCode(this AppLanguage language) => language switch
    {
        AppLanguage.En => CodeEn,
        AppLanguage.Kk => CodeKk,
        _ => CodeRu
    };

    public static string GetDisplayName(this AppLanguage language) => language switch
    {
        AppLanguage.En => "English",
        AppLanguage.Kk => "Қазақша",
        _ => "Русский"
    };

    public static AppLanguage FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return AppLanguage.Ru;

        string normalized = code.Trim().ToLowerInvariant();
        if (normalized.StartsWith(CodeEn, StringComparison.OrdinalIgnoreCase)) return AppLanguage.En;
        if (normalized.StartsWith(CodeKk, StringComparison.OrdinalIgnoreCase)) return AppLanguage.Kk;
        if (normalized.StartsWith(CodeRu, StringComparison.OrdinalIgnoreCase)) return AppLanguage.Ru;

        return AppLanguage.Ru;
    }
}
