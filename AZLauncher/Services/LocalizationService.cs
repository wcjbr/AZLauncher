using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLauncher.Models;

namespace AZLauncher.Services;

public sealed partial class LocalizationService : ObservableObject
{
    private readonly AppConfigService configService;

    public LocalizationService(AppConfigService configService)
    {
        this.configService = configService;
        currentLanguage = configService.CurrentLanguage;

        AvailableLanguages =
        [
            new LanguageOption(AppLanguage.English, "English"),
            new LanguageOption(AppLanguage.ChineseSimplified, "简体中文"),
        ];
    }

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    [ObservableProperty]
    private AppLanguage currentLanguage;

    public bool IsChinese => CurrentLanguage == AppLanguage.ChineseSimplified;

    public event EventHandler? LanguageChanged;

    partial void OnCurrentLanguageChanged(AppLanguage value)
    {
        configService.CurrentLanguage = value;
        OnPropertyChanged(nameof(IsChinese));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
