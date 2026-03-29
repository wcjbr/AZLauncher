using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class OverviewPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;

    public OverviewPageViewModel(LocalizationService localizer, LauncherStateService launcherState) : base(localizer)
    {
        this.launcherState = launcherState;
        this.launcherState.PropertyChanged += HandleLauncherStateChanged;
        RefreshCollections();
        _ = this.launcherState.RefreshAvailableGameVersionsAsync();
    }

    public string ActiveProfile => IsChinese ? launcherState.ActiveProfileNameZh : launcherState.ActiveProfileName;

    public string FeaturedVersionName => IsChinese ? launcherState.CurrentInstance.GetNickZh() : launcherState.CurrentInstance.GetNick();

    public string JavaRuntime => launcherState.JavaRuntime;

    public string MemoryAllocation => launcherState.FormatMemory(IsChinese);

    public string InstallPath => launcherState.InstallPath;

    public string LaunchState => IsChinese ? launcherState.LaunchStateZh : launcherState.LaunchState;

    public int InstalledBuilds => launcherState.InstalledBuildCount;

    public int ModCount => launcherState.ModCount;

    public int ScreenshotCount => launcherState.ScreenshotCount;

    public int FriendsOnline => launcherState.FriendsOnline;

    public string WelcomeTitle => IsChinese ? "准备进入下一段世界" : "Forge your next world";

    public string WelcomeSummary => IsChinese
        ? "在一个桌面启动器里集中管理整合包、快照版本、存档和启动准备。"
        : "Manage modpacks, snapshots, saves, and launch prep from one focused desktop launcher.";

    public string FeaturedVersionSummary => IsChinese
        ? launcherState.CurrentInstance.GetSummaryZh()
        : launcherState.CurrentInstance.GetSummary();

    public string SelectedVersionLabel => IsChinese ? "当前版本" : "Selected version";

    public string InstallModsLabel => IsChinese ? "安装模组" : "Install mods";

    public string RefreshVersionsLabel => IsChinese ? "刷新版本" : "Refresh versions";

    public string DownloadGameLabel => IsChinese ? "下载游戏" : "Download game";

    public string LaunchGameLabel => IsChinese ? "启动游戏" : "Launch game";

    public string RuntimeLabel => IsChinese ? "运行环境" : "Runtime";

    public string LaunchStatusLabel => IsChinese ? "启动状态" : "Launch status";

    public string LaunchStatusSummary => IsChinese
        ? "配置文件已索引，资源已缓存，当前版本已经可以直接启动。"
        : "Profiles are indexed, assets are cached, and the selected build is ready to start.";

    public string InstalledVersionsLabel => IsChinese ? "已安装版本" : "Installed versions";

    public string InstalledVersionsHeading => IsChinese ? "快速切换不同世界" : "Jump between worlds fast";

    public string LauncherFeedLabel => IsChinese ? "启动器动态" : "Launcher feed";

    public string LauncherFeedHeading => IsChinese ? "最近更新" : "Recent updates";

    public string BuildsLabel => IsChinese ? "版本" : "Builds";

    public string ModsLabel => IsChinese ? "模组" : "Mods";

    public string ShotsLabel => IsChinese ? "截图" : "Shots";

    public string FriendsLabel => IsChinese ? "好友" : "Friends";

    public string RecommendedLabel => IsChinese ? "推荐" : "Recommended";

    public string DownloadVersionLabel => IsChinese ? "可下载版本" : "Downloadable versions";

    public string OperationLabel => IsChinese ? "当前任务" : "Current task";

    public string LaunchPreviewLabel => IsChinese ? "启动命令" : "Launch command";

    public IReadOnlyList<DownloadableGameVersion> AvailableGameVersions => launcherState.AvailableGameVersions;

    public DownloadableGameVersion? SelectedDownloadVersion
    {
        get => launcherState.SelectedDownloadVersion;
        set => launcherState.SelectedDownloadVersion = value;
    }

    public double OperationProgress => launcherState.OperationProgress;

    public string OperationDetail => launcherState.OperationDetail;

    public string LastLaunchCommandPreview => launcherState.LastLaunchCommandPreview;

    public bool HasLaunchCommandPreview => !string.IsNullOrWhiteSpace(LastLaunchCommandPreview);

    public bool IsBusy => launcherState.IsBusy;

    [ObservableProperty]
    private IReadOnlyList<LauncherVersion> installedVersions = [];

    [ObservableProperty]
    private IReadOnlyList<NewsItem> newsItems = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(MemoryAllocation),
            nameof(ActiveProfile),
            nameof(FeaturedVersionName),
            nameof(JavaRuntime),
            nameof(InstallPath),
            nameof(LaunchState),
            nameof(InstalledBuilds),
            nameof(ModCount),
            nameof(ScreenshotCount),
            nameof(FriendsOnline),
            nameof(WelcomeTitle),
            nameof(WelcomeSummary),
            nameof(FeaturedVersionSummary),
            nameof(SelectedVersionLabel),
            nameof(InstallModsLabel),
            nameof(RefreshVersionsLabel),
            nameof(DownloadGameLabel),
            nameof(LaunchGameLabel),
            nameof(RuntimeLabel),
            nameof(LaunchStatusLabel),
            nameof(LaunchStatusSummary),
            nameof(InstalledVersionsLabel),
            nameof(InstalledVersionsHeading),
            nameof(LauncherFeedLabel),
            nameof(LauncherFeedHeading),
            nameof(BuildsLabel),
            nameof(ModsLabel),
            nameof(ShotsLabel),
            nameof(FriendsLabel),
            nameof(RecommendedLabel),
            nameof(DownloadVersionLabel),
            nameof(OperationLabel),
            nameof(LaunchPreviewLabel));
    }

    private void RefreshCollections()
    {
        InstalledVersions = launcherState.GetLocalizedVersions(IsChinese);

        NewsItems = IsChinese ?
        [
            new NewsItem
            {
                Category = "补丁说明",
                Title = "春季配置刷新",
                Summary = "更新了默认光影预设、重建基础键位包，并缩短了约 18% 启动耗时。",
            },
            new NewsItem
            {
                Category = "资源库",
                Title = "新增整合包暂存区",
                Summary = "现在可以先区分稳定版和预览版整合包，再决定是否真正安装。",
            },
            new NewsItem
            {
                Category = "备份",
                Title = "世界快照校验完成",
                Summary = "最近一次自动备份已经成功归档三个世界，并通过校验和验证。",
            },
        ]
        :
        [
            new NewsItem
            {
                Category = "Patch Notes",
                Title = "Spring profile refresh",
                Summary = "Updated the default shader preset, rebuilt keybind packs, and trimmed startup time by 18 percent.",
            },
            new NewsItem
            {
                Category = "Library",
                Title = "New staging area for modpacks",
                Summary = "Separate stable and preview packs before installing larger updates into your main profile.",
            },
            new NewsItem
            {
                Category = "Backups",
                Title = "World snapshot verification completed",
                Summary = "The latest automated backup archived three worlds and passed checksum validation.",
            },
        ];
    }

    [RelayCommand]
    private Task RefreshVersionsAsync()
    {
        return launcherState.RefreshAvailableGameVersionsAsync();
    }

    [RelayCommand]
    private Task DownloadGameAsync()
    {
        return launcherState.DownloadSelectedGameAsync();
    }

    [RelayCommand]
    private Task LaunchGameAsync()
    {
        return launcherState.LaunchCurrentGameAsync();
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherStateService.CurrentVersionId)
            or nameof(LauncherStateService.InstallPath)
            or nameof(LauncherStateService.JavaRuntime)
            or nameof(LauncherStateService.AllocatedMemoryGb)
            or nameof(LauncherStateService.InstalledBuildCount)
            or nameof(LauncherStateService.ModCount)
            or nameof(LauncherStateService.ScreenshotCount)
            or nameof(LauncherStateService.FriendsOnline)
            or nameof(LauncherStateService.AvailableStorageBytes)
            or nameof(LauncherStateService.AvailableGameVersions)
            or nameof(LauncherStateService.SelectedDownloadVersion)
            or nameof(LauncherStateService.OperationProgress)
            or nameof(LauncherStateService.OperationDetail)
            or nameof(LauncherStateService.LastLaunchCommandPreview)
            or nameof(LauncherStateService.IsBusy))
        {
            RefreshCollections();
            RaiseProperties(
                nameof(ActiveProfile),
                nameof(FeaturedVersionName),
                nameof(JavaRuntime),
                nameof(MemoryAllocation),
                nameof(InstallPath),
                nameof(LaunchState),
                nameof(InstalledBuilds),
                nameof(ModCount),
                nameof(ScreenshotCount),
                nameof(FriendsOnline),
                nameof(FeaturedVersionSummary),
                nameof(AvailableGameVersions),
                nameof(SelectedDownloadVersion),
                nameof(OperationProgress),
                nameof(OperationDetail),
                nameof(LastLaunchCommandPreview),
                nameof(HasLaunchCommandPreview),
                nameof(IsBusy));
        }
    }
}
