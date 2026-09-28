using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PingArmor.Localization;

namespace PingArmor.UI.ViewModels;

/// <summary>
/// Base class for view models. Exposes <see cref="Texts"/> for XAML bindings and keeps
/// them fresh when the application language changes.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    protected ViewModelBase()
    {
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Localized texts, e.g. <c>{Binding Texts.NavOverview}</c>.</summary>
    public LocalizedStrings Texts => LocalizationService.Strings;

    private void OnLanguageChanged()
    {
        // Empty name invalidates every binding on this view model, including Texts.*.
        OnPropertyChanged(string.Empty);
        OnLanguageChangedCore();
    }

    /// <summary>Override to refresh computed state after a language change.</summary>
    protected virtual void OnLanguageChangedCore() { }

    public virtual void Dispose()
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }
}