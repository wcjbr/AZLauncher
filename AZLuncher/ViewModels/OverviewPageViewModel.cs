using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class OverviewPageViewModel : LocalizedViewModelBase
{
    public OverviewPageViewModel(LocalizationService localizer) : base(localizer)
    {
        RefreshCollections();
    }

    public string ActiveProfile => "Expedition Pack";

    public string FeaturedVersionName => "Fabric 1.21.4";

    public string JavaRuntime => "Java 21 Temurin";

    public string MemoryAllocation => IsChinese ? "已分配 6 GB" : "6 GB allocated";

    public string InstallPath => "/home/archzero/Games/Minecraft";

    public string LaunchState => IsChinese ? "可以启动" : "Ready to launch";

    public int InstalledBuilds => 8;

    public int ModCount => 42;

    public int ScreenshotCount => 186;

    public int FriendsOnline => 7;

    public string WelcomeTitle => IsChinese ? "准备进入下一段世界" : "Forge your next world";

    public string WelcomeSummary => IsChinese
        ? "在一个桌面启动器里集中管理整合包、快照版本、存档和启动准备。"
        : "Manage modpacks, snapshots, saves, and launch prep from one focused desktop launcher.";

    public string FeaturedVersionSummary => IsChinese
        ? "主力客户端配置，包含光影、地图工具和常用体验优化模组。"
        : "Primary client setup with shaders, map tools, and quality-of-life mods.";

    public string SelectedVersionLabel => IsChinese ? "当前版本" : "Selected version";

    public string InstallModsLabel => IsChinese ? "安装模组" : "Install mods";

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

    [ObservableProperty]
    private IReadOnlyList<LauncherVersion> installedVersions = [];

    [ObservableProperty]
    private IReadOnlyList<NewsItem> newsItems = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(MemoryAllocation),
            nameof(LaunchState),
            nameof(WelcomeTitle),
            nameof(WelcomeSummary),
            nameof(FeaturedVersionSummary),
            nameof(SelectedVersionLabel),
            nameof(InstallModsLabel),
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
            nameof(RecommendedLabel));
    }

    private void RefreshCollections()
    {
        InstalledVersions = IsChinese ?
        [
            new LauncherVersion
            {
                Name = "Fabric 1.21.4",
                Channel = "主力",
                Summary = "轻量生存配置，适合日常游玩和夜间光影测试。",
                LastPlayed = "2 小时前游玩",
                IsRecommended = true,
                BadgeText = "推荐",
            },
            new LauncherVersion
            {
                Name = "NeoForge 1.20.1",
                Channel = "整合",
                Summary = "偏重工业和自动化的大型模组组合，带额外地形与手柄支持。",
                LastPlayed = "昨天游玩",
            },
            new LauncherVersion
            {
                Name = "Snapshot 25w13a",
                Channel = "实验",
                Summary = "用于测试新特性的快照实例，适合单独隔离试玩。",
                LastPlayed = "4 天前游玩",
            },
        ]
        :
        [
            new LauncherVersion
            {
                Name = "Fabric 1.21.4",
                Channel = "Primary",
                Summary = "Fast survival profile for daily play and shader testing.",
                LastPlayed = "Played 2 hours ago",
                IsRecommended = true,
                BadgeText = "Recommended",
            },
            new LauncherVersion
            {
                Name = "NeoForge 1.20.1",
                Channel = "Modded",
                Summary = "Heavy automation stack with expanded world generation and controller support.",
                LastPlayed = "Played yesterday",
            },
            new LauncherVersion
            {
                Name = "Snapshot 25w13a",
                Channel = "Experimental",
                Summary = "Disposable testing instance for preview features and config checks.",
                LastPlayed = "Played 4 days ago",
            },
        ];

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
}
