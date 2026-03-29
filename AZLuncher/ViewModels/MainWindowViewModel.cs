using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public enum LauncherSection
{
    Overview,
    Instances,
    Library,
    Backups,
    Customize,
}

public sealed partial class MainWindowViewModel : LocalizedViewModelBase
{
    private readonly OverviewPageViewModel overviewPage;
    private readonly InstancesPageViewModel instancesPage;
    private readonly LibraryPageViewModel libraryPage;
    private readonly BackupsPageViewModel backupsPage;
    private readonly CustomizePageViewModel customizePage;
    private readonly AppConfigService configService;
    private bool suppressLanguageSync;

    public MainWindowViewModel(AppConfigService configService) : base(new LocalizationService(configService))
    {
        this.configService = configService;
        this.configService.PropertyChanged += HandleConfigPropertyChanged;
        var themeCustomizationService = new ThemeCustomizationService(configService);
        overviewPage = new OverviewPageViewModel(Localizer);
        instancesPage = new InstancesPageViewModel(Localizer);
        libraryPage = new LibraryPageViewModel(Localizer);
        backupsPage = new BackupsPageViewModel(Localizer);
        customizePage = new CustomizePageViewModel(Localizer, themeCustomizationService, configService);

        Languages = Localizer.AvailableLanguages;
        suppressLanguageSync = true;
        SelectedLanguage = Languages.FirstOrDefault(option => option.Language == Localizer.CurrentLanguage)
            ?? Languages[0];
        suppressLanguageSync = false;

        CurrentPage = overviewPage;
        CurrentSection = LauncherSection.Overview;
    }

    public string PlayerName => "ArchZero";

    public string ActiveProfile => "Expedition Pack";

    public string StorageValue => IsChinese ? "128 GB 可用" : "128 GB free";

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    private LanguageOption selectedLanguage;

    [ObservableProperty]
    private ViewModelBase currentPage;

    [ObservableProperty]
    private LauncherSection currentSection;

    public string WindowTitle => configService.LauncherTitle;

    public string SidebarTitle => configService.LauncherTitle;

    public string SidebarSummary => IsChinese
        ? "一个偏重实例、模组和备份管理的桌面启动器壳层。"
        : "A focused desktop shell for instances, mods, backups, and launch prep.";

    public string PlayerLabel => IsChinese ? "玩家" : "Player";

    public string LaunchState => IsChinese ? "可以启动" : "Ready to launch";

    public string OverviewNavLabel => IsChinese ? "概览" : "Overview";

    public string InstancesNavLabel => IsChinese ? "实例" : "Instances";

    public string LibraryNavLabel => IsChinese ? "模组库" : "Mod library";

    public string BackupsNavLabel => IsChinese ? "备份" : "World backups";

    public string CustomizeNavLabel => IsChinese ? "自定义" : "Customize";

    public string StorageLabel => IsChinese ? "存储空间" : "Storage";

    public string StorageSummary => IsChinese
        ? "保留足够空间用于整合包下载、缓存和世界轮换备份。"
        : "Enough room for modpacks, caches, and world backup rotation.";

    public string LanguageLabel => IsChinese ? "语言" : "Language";

    public string HeaderEyebrow => CurrentSection switch
    {
        LauncherSection.Overview => IsChinese ? "启动总览" : "Launch overview",
        LauncherSection.Instances => IsChinese ? "实例管理" : "Instance management",
        LauncherSection.Library => IsChinese ? "资源整理" : "Library organization",
        LauncherSection.Backups => IsChinese ? "世界保护" : "World protection",
        LauncherSection.Customize => IsChinese ? "界面定制" : "Interface tuning",
        _ => string.Empty,
    };

    public string HeaderTitle => CurrentSection switch
    {
        LauncherSection.Overview => IsChinese ? "开始之前先看全局状态" : "See the launcher state before you play",
        LauncherSection.Instances => IsChinese ? "在多个实例之间稳定切换" : "Move cleanly between multiple instances",
        LauncherSection.Library => IsChinese ? "把模组和资源分类整理" : "Sort mods and content before install",
        LauncherSection.Backups => IsChinese ? "把恢复点留在安全位置" : "Keep restore points ready and visible",
        LauncherSection.Customize => IsChinese ? "把这套启动器改成你的风格" : "Shape the launcher around your own style",
        _ => string.Empty,
    };

    public string HeaderSummary => CurrentSection switch
    {
        LauncherSection.Overview => IsChinese
            ? "这里显示启动准备、已安装版本和最近的启动器动态。"
            : "This page shows launch readiness, installed builds, and recent launcher updates.",
        LauncherSection.Instances => IsChinese
            ? "在这里管理不同实例、版本链路和预设配置。"
            : "Manage separate instances, launch chains, and runtime presets here.",
        LauncherSection.Library => IsChinese
            ? "把模组、资源包和待审核内容先整理，再接入真实安装流程。"
            : "Organize mods, packs, and review candidates before wiring in real install flows.",
        LauncherSection.Backups => IsChinese
            ? "查看备份策略、快照列表和未来的恢复入口。"
            : "Track backup policy, snapshots, and the future restore entry point.",
        LauncherSection.Customize => IsChinese
            ? "这里可以定制主题配色、界面密度和圆角节奏，而且会立即生效。"
            : "Customize theme color, interface density, and corner rhythm here with immediate visual updates.",
        _ => string.Empty,
    };

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (suppressLanguageSync)
        {
            return;
        }

        if (Localizer.CurrentLanguage != value.Language)
        {
            Localizer.CurrentLanguage = value.Language;
        }
    }

    protected override void OnLanguageChanged()
    {
        suppressLanguageSync = true;
        SelectedLanguage = Languages.FirstOrDefault(option => option.Language == Localizer.CurrentLanguage) ?? Languages[0];
        suppressLanguageSync = false;
        RaiseProperties(
            nameof(SidebarTitle),
            nameof(SidebarSummary),
            nameof(PlayerLabel),
            nameof(LaunchState),
            nameof(OverviewNavLabel),
            nameof(InstancesNavLabel),
            nameof(LibraryNavLabel),
            nameof(BackupsNavLabel),
            nameof(CustomizeNavLabel),
            nameof(StorageLabel),
            nameof(StorageValue),
            nameof(StorageSummary),
            nameof(LanguageLabel),
            nameof(HeaderEyebrow),
            nameof(HeaderTitle),
            nameof(HeaderSummary));
    }

    [RelayCommand]
    private void NavigateOverview()
    {
        CurrentSection = LauncherSection.Overview;
        CurrentPage = overviewPage;
        RaiseHeaderProperties();
    }

    [RelayCommand]
    private void NavigateInstances()
    {
        CurrentSection = LauncherSection.Instances;
        CurrentPage = instancesPage;
        RaiseHeaderProperties();
    }

    [RelayCommand]
    private void NavigateLibrary()
    {
        CurrentSection = LauncherSection.Library;
        CurrentPage = libraryPage;
        RaiseHeaderProperties();
    }

    [RelayCommand]
    private void NavigateBackups()
    {
        CurrentSection = LauncherSection.Backups;
        CurrentPage = backupsPage;
        RaiseHeaderProperties();
    }

    [RelayCommand]
    private void NavigateCustomize()
    {
        CurrentSection = LauncherSection.Customize;
        CurrentPage = customizePage;
        RaiseHeaderProperties();
    }

    private void RaiseHeaderProperties()
    {
        RaiseProperties(nameof(HeaderEyebrow), nameof(HeaderTitle), nameof(HeaderSummary));
    }

    private void HandleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfigService.LauncherTitle))
        {
            RaiseProperties(nameof(WindowTitle), nameof(SidebarTitle));
        }
    }
}
