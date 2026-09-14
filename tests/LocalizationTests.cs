using System;
using System.IO;
using System.Reflection;
using PingArmor.Config;
using PingArmor.Localization;
using Xunit;

namespace PingArmor.Tests;

public class LocalizationTests : IDisposable
{
    private readonly string _tempConfigPath;

    public LocalizationTests()
    {
        _tempConfigPath = Path.Combine(Path.GetTempPath(), $"test_loc_config_{Guid.NewGuid()}.json");
    }

    [Fact]
    public void LocalizedStrings_AllLanguagePacks_HaveAllNonEmptyProperties()
    {
        var packs = new[]
        {
            (AppLanguage.Ru, LocalizedStrings.Ru),
            (AppLanguage.En, LocalizedStrings.En),
            (AppLanguage.Kk, LocalizedStrings.Kk)
        };

        var stringProperties = typeof(LocalizedStrings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var (lang, pack) in packs)
        {
            foreach (var prop in stringProperties)
            {
                if (prop.PropertyType == typeof(string))
                {
                    var value = (string?)prop.GetValue(pack);
                    Assert.False(
                        string.IsNullOrWhiteSpace(value),
                        $"Property '{prop.Name}' in language '{lang}' must not be null or whitespace."
                    );
                }
            }
        }
    }

    [Theory]
    [InlineData("ru", AppLanguage.Ru)]
    [InlineData("RU", AppLanguage.Ru)]
    [InlineData("ru-RU", AppLanguage.Ru)]
    [InlineData("en", AppLanguage.En)]
    [InlineData("EN", AppLanguage.En)]
    [InlineData("en-US", AppLanguage.En)]
    [InlineData("kk", AppLanguage.Kk)]
    [InlineData("KK", AppLanguage.Kk)]
    [InlineData("kk-KZ", AppLanguage.Kk)]
    [InlineData("", AppLanguage.Ru)]
    [InlineData(null, AppLanguage.Ru)]
    [InlineData("fr", AppLanguage.Ru)]
    public void AppLanguageExtensions_FromCode_ParsesCorrectly(string? input, AppLanguage expected)
    {
        var result = AppLanguageExtensions.FromCode(input);
        Assert.Equal(expected, result);
    }

    private static readonly object TestLock = new();

    [Fact]
    public void LocalizationService_SetLanguage_FiresEventAndUpdatesStrings()
    {
        lock (TestLock)
        {
            // Reset to RU
            LocalizationService.SetLanguage(AppLanguage.Ru);

            bool eventFired = false;
            Action handler = () => eventFired = true;
            LocalizationService.LanguageChanged += handler;

            try
            {
                // Switch to EN
                LocalizationService.SetLanguage(AppLanguage.En);

                Assert.True(eventFired);
                Assert.Equal(AppLanguage.En, LocalizationService.CurrentLanguage);
                Assert.Equal(LocalizedStrings.En.Exit, LocalizationService.Strings.Exit);

                // Repeated invocation with the same language should not fire event
                eventFired = false;
                LocalizationService.SetLanguage(AppLanguage.En);
                Assert.False(eventFired);

                // Switch to KK
                LocalizationService.SetLanguage(AppLanguage.Kk);
                Assert.True(eventFired);
                Assert.Equal(AppLanguage.Kk, LocalizationService.CurrentLanguage);
                Assert.Equal(LocalizedStrings.Kk.Exit, LocalizationService.Strings.Exit);
            }
            finally
            {
                LocalizationService.LanguageChanged -= handler;
                LocalizationService.SetLanguage(AppLanguage.Ru);
            }
        }
    }

    [Fact]
    public void AppConfig_Language_DefaultsToRu_AndPersistsAcrossSaveLoad()
    {
        var defaultConfig = AppConfig.Load(_tempConfigPath);
        Assert.Equal("ru", defaultConfig.Language);

        defaultConfig.Language = "kk";
        defaultConfig.Save(_tempConfigPath);

        var loadedConfig = AppConfig.Load(_tempConfigPath);
        Assert.Equal("kk", loadedConfig.Language);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempConfigPath))
            {
                File.Delete(_tempConfigPath);
            }
        }
        catch { }
    }
}
