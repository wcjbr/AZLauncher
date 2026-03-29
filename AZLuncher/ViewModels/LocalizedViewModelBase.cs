using System;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public abstract class LocalizedViewModelBase : ViewModelBase
{
    protected LocalizedViewModelBase(LocalizationService localizer)
    {
        Localizer = localizer;
        Localizer.LanguageChanged += HandleLanguageChanged;
    }

    public LocalizationService Localizer { get; }

    protected bool IsChinese => Localizer.IsChinese;

    protected virtual void OnLanguageChanged()
    {
    }

    protected void RaiseProperties(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void HandleLanguageChanged(object? sender, EventArgs e)
    {
        OnLanguageChanged();
    }
}
