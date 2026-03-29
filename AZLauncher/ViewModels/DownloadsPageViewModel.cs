using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLauncher.Models;
using AZLauncher.Services;

namespace AZLauncher.ViewModels;

public sealed partial class DownloadsPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;
    private readonly DownloadCenterService downloadCenterService;

    public DownloadsPageViewModel(LocalizationService localizer, LauncherStateService launcherState) : base(localizer)
    {
        this.launcherState = launcherState;
        this.launcherState.PropertyChanged += HandleLauncherStateChanged;
        downloadCenterService = new DownloadCenterService();
        Sources =
        [
            new DownloadSourceOption(DownloadSource.Official, "Official / 官方"),
            new DownloadSourceOption(DownloadSource.BMCLAPI, "BMCLAPI"),
        ];

        selectedGameSource = Sources[0];
        selectedLoaderSource = Sources[1];
        _ = RefreshGameVersionsAsync();
        _ = RefreshLoaderVersionsAsync();
    }

    public IReadOnlyList<DownloadSourceOption> Sources { get; }

    public IReadOnlyList<LoaderKind> LoaderKinds { get; } = [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge];

    public IReadOnlyList<ResourceContentType> ResourceKinds { get; } =
        [ResourceContentType.Mod, ResourceContentType.ResourcePack, ResourceContentType.ShaderPack];

    public string SectionLabel => IsChinese ? "下载中心" : "Downloads";

    public string SectionHeading => IsChinese ? "集中下载游戏、加载器与资源" : "Download games, loaders, and assets in one place";

    public string SectionSummary => IsChinese
        ? "这里单独处理 Minecraft 版本、Fabric/Forge/NeoForge 安装器，以及模组、资源包和光影文件。"
        : "This page handles Minecraft versions, Fabric/Forge/NeoForge installers, and mods, packs, and shaders.";

    public string TabsLabel => IsChinese ? "下载分类" : "Download categories";

    public string GamesTabLabel => IsChinese ? "游戏" : "Games";

    public string LoadersTabLabel => IsChinese ? "加载器" : "Loaders";

    public string ResourcesTabLabel => IsChinese ? "资源" : "Resources";

    public string SourceLabel => IsChinese ? "下载源" : "Source";

    public string GameVersionLabel => IsChinese ? "游戏版本" : "Game version";

    public string RefreshLabel => IsChinese ? "刷新列表" : "Refresh";

    public string DownloadGameLabel => IsChinese ? "下载游戏" : "Download game";

    public string LoaderKindLabel => IsChinese ? "加载器类型" : "Loader type";

    public string LoaderVersionLabel => IsChinese ? "加载器版本" : "Loader version";

    public string DownloadLoaderLabel => IsChinese ? "下载加载器" : "Download loader";

    public string ResourceKindLabel => IsChinese ? "资源类型" : "Resource type";

    public string ResourceUrlLabel => IsChinese ? "资源直链" : "Resource URL";

    public string ResourceNameLabel => IsChinese ? "文件名" : "File name";

    public string DownloadResourceLabel => IsChinese ? "下载资源" : "Download resource";

    public string CurrentTargetLabel => IsChinese ? "当前实例" : "Current instance";

    public string ActivityLabel => IsChinese ? "下载状态" : "Activity";

    public string ActiveSectionTitle => CurrentSection switch
    {
        DownloadSection.Games => IsChinese ? "下载原版游戏文件" : "Download vanilla game files",
        DownloadSection.Loaders => IsChinese ? "下载加载器安装器" : "Download loader installers",
        DownloadSection.Resources => IsChinese ? "下载模组与视觉资源" : "Download mods and visual assets",
        _ => string.Empty,
    };

    public string ActiveSectionSummary => CurrentSection switch
    {
        DownloadSection.Games => IsChinese
            ? "支持官方源和 BMCLAPI 镜像下载 Minecraft 版本。"
            : "Download Minecraft versions from the official source or BMCLAPI mirror.",
        DownloadSection.Loaders => IsChinese
            ? "支持 Fabric、Forge 和 NeoForge 安装器下载。"
            : "Download Fabric, Forge, and NeoForge installers.",
        DownloadSection.Resources => IsChinese
            ? "把模组、资源包或光影直链下载到当前实例目录。"
            : "Download mods, resource packs, or shaders directly into the current instance directory.",
        _ => string.Empty,
    };

    public string CurrentInstanceName => IsChinese ? launcherState.ActiveProfileNameZh : launcherState.ActiveProfileName;

    public string CurrentTargetPath => launcherState.InstallPath;

    public bool IsGamesSelected => CurrentSection == DownloadSection.Games;

    public bool IsLoadersSelected => CurrentSection == DownloadSection.Loaders;

    public bool IsResourcesSelected => CurrentSection == DownloadSection.Resources;

    public bool IsBusy => IsRefreshingGames || IsDownloadingGame || IsRefreshingLoaders || IsDownloadingLoader || IsDownloadingResource;

    [ObservableProperty]
    private DownloadSection currentSection = DownloadSection.Games;

    [ObservableProperty]
    private DownloadSourceOption selectedGameSource;

    [ObservableProperty]
    private DownloadSourceOption selectedLoaderSource;

    [ObservableProperty]
    private IReadOnlyList<DownloadableGameVersion> availableGameVersions = [];

    [ObservableProperty]
    private DownloadableGameVersion? selectedGameVersion;

    [ObservableProperty]
    private LoaderKind selectedLoaderKind = LoaderKind.Fabric;

    [ObservableProperty]
    private IReadOnlyList<DownloadableLoaderVersion> availableLoaderVersions = [];

    [ObservableProperty]
    private DownloadableLoaderVersion? selectedLoaderVersion;

    [ObservableProperty]
    private ResourceContentType selectedResourceKind = ResourceContentType.Mod;

    [ObservableProperty]
    private string resourceUrl = string.Empty;

    [ObservableProperty]
    private string resourceFileName = string.Empty;

    [ObservableProperty]
    private bool isRefreshingGames;

    [ObservableProperty]
    private bool isDownloadingGame;

    [ObservableProperty]
    private bool isRefreshingLoaders;

    [ObservableProperty]
    private bool isDownloadingLoader;

    [ObservableProperty]
    private bool isDownloadingResource;

    [ObservableProperty]
    private double activityProgress;

    [ObservableProperty]
    private string activityMessage = string.Empty;

    partial void OnIsRefreshingGamesChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsDownloadingGameChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsRefreshingLoadersChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsDownloadingLoaderChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsDownloadingResourceChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

    partial void OnCurrentSectionChanged(DownloadSection value)
    {
        RaiseProperties(nameof(IsGamesSelected), nameof(IsLoadersSelected), nameof(IsResourcesSelected),
            nameof(ActiveSectionTitle), nameof(ActiveSectionSummary));
    }

    partial void OnSelectedGameSourceChanged(DownloadSourceOption value)
    {
        _ = RefreshGameVersionsAsync();
    }

    partial void OnSelectedLoaderSourceChanged(DownloadSourceOption value)
    {
        _ = RefreshLoaderVersionsAsync();
    }

    partial void OnSelectedLoaderKindChanged(LoaderKind value)
    {
        _ = RefreshLoaderVersionsAsync();
    }

    partial void OnSelectedGameVersionChanged(DownloadableGameVersion? value)
    {
        _ = RefreshLoaderVersionsAsync();
    }

    protected override void OnLanguageChanged()
    {
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SectionSummary),
            nameof(TabsLabel),
            nameof(GamesTabLabel),
            nameof(LoadersTabLabel),
            nameof(ResourcesTabLabel),
            nameof(SourceLabel),
            nameof(GameVersionLabel),
            nameof(RefreshLabel),
            nameof(DownloadGameLabel),
            nameof(LoaderKindLabel),
            nameof(LoaderVersionLabel),
            nameof(DownloadLoaderLabel),
            nameof(ResourceKindLabel),
            nameof(ResourceUrlLabel),
            nameof(ResourceNameLabel),
            nameof(DownloadResourceLabel),
            nameof(CurrentTargetLabel),
            nameof(ActivityLabel),
            nameof(ActiveSectionTitle),
            nameof(ActiveSectionSummary),
            nameof(CurrentInstanceName),
            nameof(CurrentTargetPath));
    }

    [RelayCommand]
    private void ShowGames() => CurrentSection = DownloadSection.Games;

    [RelayCommand]
    private void ShowLoaders() => CurrentSection = DownloadSection.Loaders;

    [RelayCommand]
    private void ShowResources() => CurrentSection = DownloadSection.Resources;

    [RelayCommand]
    private async Task RefreshGameVersionsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsRefreshingGames = true;
            ActivityProgress = 5;
            ActivityMessage = IsChinese ? "正在刷新游戏版本列表..." : "Refreshing game version list...";
            await RefreshGameVersionsCoreAsync();
            SelectedGameVersion = AvailableGameVersions.FirstOrDefault(version => !version.IsInstalled)
                                  ?? AvailableGameVersions.FirstOrDefault();
            ActivityProgress = 100;
            ActivityMessage = IsChinese ? "游戏版本列表已刷新" : "Game version list refreshed";
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
        }
        finally
        {
            IsRefreshingGames = false;
        }
    }

    [RelayCommand]
    private async Task DownloadGameAsync()
    {
        if (IsBusy || SelectedGameVersion is null)
        {
            return;
        }

        try
        {
            IsDownloadingGame = true;
            var progress = new Progress<RuntimeProgressUpdate>(update =>
            {
                ActivityProgress = update.Progress;
                ActivityMessage = update.Message;
            });

            await downloadCenterService.DownloadGameAsync(
                SelectedGameVersion.Id,
                launcherState.InstallPath,
                SelectedGameSource.Source,
                progress);

            ActivityMessage = IsChinese
                ? $"已下载游戏版本 {SelectedGameVersion.Id}"
                : $"Downloaded game version {SelectedGameVersion.Id}";
            await RefreshGameVersionsCoreAsync();
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
        }
        finally
        {
            IsDownloadingGame = false;
        }
    }

    [RelayCommand]
    private async Task RefreshLoaderVersionsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsRefreshingLoaders = true;
            ActivityProgress = 5;
            ActivityMessage = IsChinese ? "正在刷新加载器版本..." : "Refreshing loader versions...";
            await RefreshLoaderVersionsCoreAsync();
            SelectedLoaderVersion = AvailableLoaderVersions.FirstOrDefault();
            ActivityProgress = 100;
            ActivityMessage = IsChinese ? "加载器版本列表已刷新" : "Loader version list refreshed";
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
        }
        finally
        {
            IsRefreshingLoaders = false;
        }
    }

    [RelayCommand]
    private async Task DownloadLoaderAsync()
    {
        if (IsBusy || SelectedLoaderVersion is null)
        {
            return;
        }

        try
        {
            IsDownloadingLoader = true;
            ActivityProgress = 15;
            ActivityMessage = IsChinese ? "正在下载加载器..." : "Downloading loader...";
            var targetPath = await downloadCenterService.DownloadLoaderAsync(
                SelectedLoaderKind,
                SelectedLoaderSource.Source,
                launcherState.InstallPath,
                SelectedLoaderVersion);
            ActivityProgress = 100;
            ActivityMessage = IsChinese
                ? $"已保存到 {targetPath}"
                : $"Saved to {targetPath}";
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
        }
        finally
        {
            IsDownloadingLoader = false;
        }
    }

    [RelayCommand]
    private async Task DownloadResourceAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsDownloadingResource = true;
            ActivityProgress = 15;
            ActivityMessage = IsChinese ? "正在下载资源..." : "Downloading resource...";
            var targetPath = await downloadCenterService.DownloadResourceAsync(
                SelectedResourceKind,
                ResourceUrl,
                ResourceFileName,
                launcherState.InstallPath,
                launcherState.CurrentInstance.GetId());
            ActivityProgress = 100;
            ActivityMessage = IsChinese
                ? $"资源已保存到 {targetPath}"
                : $"Resource saved to {targetPath}";
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
        }
        finally
        {
            IsDownloadingResource = false;
        }
    }

    private Task RefreshGameVersionsCoreAsync()
    {
        return RefreshGameVersionsCoreAsyncImpl();
    }

    private async Task RefreshGameVersionsCoreAsyncImpl()
    {
        AvailableGameVersions = await downloadCenterService.GetGameVersionsAsync(
            launcherState.InstallPath,
            SelectedGameSource.Source);
    }

    private Task RefreshLoaderVersionsCoreAsync()
    {
        return RefreshLoaderVersionsCoreAsyncImpl();
    }

    private async Task RefreshLoaderVersionsCoreAsyncImpl()
    {
        AvailableLoaderVersions = await downloadCenterService.GetLoaderVersionsAsync(
            SelectedLoaderKind,
            SelectedLoaderSource.Source,
            SelectedGameVersion?.Id ?? launcherState.CurrentInstance.GetGameVersionId());
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherStateService.CurrentVersionId) or nameof(LauncherStateService.InstallPath))
        {
            RaiseProperties(nameof(CurrentInstanceName), nameof(CurrentTargetPath));
        }
    }
}
