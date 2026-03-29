using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLauncher.Models;
using AZLauncher.Services;

namespace AZLauncher.ViewModels;

public sealed partial class DownloadsPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;
    private readonly DownloadCenterService downloadCenterService;
    private FileSystemWatcher? modsWatcher;
    private FileSystemWatcher? resourcePackWatcher;
    private FileSystemWatcher? shaderWatcher;
    private bool suppressInstanceSelectionSync;

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

        GameInstallOptions =
        [
            new GameInstallOption(GameInstallKind.Vanilla, "Vanilla / 原版"),
            new GameInstallOption(GameInstallKind.Fabric, "Fabric"),
            new GameInstallOption(GameInstallKind.Forge, "Forge"),
            new GameInstallOption(GameInstallKind.NeoForge, "NeoForge"),
        ];

        selectedGameSource = Sources[0];
        selectedLoaderSource = Sources[1];
        selectedGameInstallOption = GameInstallOptions[0];

        RefreshInstances();
        SyncSelectedInstanceFromState();
        ConfigureLocalResourceWatchers();

        ResourceStatusMessage = GetDefaultResourceStatus();
        _ = RefreshGameVersionsAsync();
        _ = RefreshLoaderVersionsCoreAsync();
        _ = RefreshLocalResourcesAsync();
    }

    public IReadOnlyList<DownloadSourceOption> Sources { get; }

    public IReadOnlyList<GameInstallOption> GameInstallOptions { get; }

    public IReadOnlyList<LoaderKind> LoaderKinds { get; } = [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge];

    public IReadOnlyList<ResourceContentType> ResourceKinds { get; } =
        [ResourceContentType.Mod, ResourceContentType.ResourcePack, ResourceContentType.ShaderPack];

    public string SectionLabel => IsChinese ? "下载中心" : "Downloads";

    public string SectionHeading => IsChinese ? "实例驱动的游戏下载与资源管理" : "Instance-driven game downloads and resource management";

    public string SectionSummary => IsChinese
        ? "选择实例后，在这里下载 Minecraft、本体加载器，并通过 Modrinth 搜索安装模组、资源包和光影。"
        : "Pick an instance, download Minecraft with a loader, and install mods, packs, and shaders from Modrinth.";

    public string TabsLabel => IsChinese ? "下载分类" : "Download categories";

    public string GamesTabLabel => IsChinese ? "游戏" : "Games";

    public string LoadersTabLabel => IsChinese ? "加载器" : "Loaders";

    public string ResourcesTabLabel => IsChinese ? "资源" : "Resources";

    public string SourceLabel => IsChinese ? "下载源" : "Source";

    public string InstanceLabel => IsChinese ? "目标实例" : "Target instance";

    public string GameVersionLabel => IsChinese ? "游戏版本" : "Game version";

    public string RefreshLabel => IsChinese ? "刷新列表" : "Refresh";

    public string DownloadGameLabel => IsChinese ? "安装到实例" : "Install to instance";

    public string LoaderKindLabel => IsChinese ? "加载器类型" : "Loader type";

    public string LoaderVersionLabel => IsChinese ? "加载器版本" : "Loader version";

    public string DownloadLoaderLabel => IsChinese ? "下载安装器" : "Download installer";

    public string ResourceKindLabel => IsChinese ? "资源类型" : "Resource type";

    public string ResourceSearchLabel => IsChinese ? "Modrinth 搜索" : "Modrinth search";

    public string ResourceSearchHint => IsChinese ? "例如 sodium、faithful、complementary" : "For example: sodium, faithful, complementary";

    public string SearchResourceLabel => IsChinese ? "搜索资源" : "Search resources";

    public string InstallResourceLabel => IsChinese ? "安装到实例" : "Install to instance";

    public string SearchResultsLabel => IsChinese ? "在线结果" : "Online results";

    public string LocalResourcesLabel => IsChinese ? "本地资源" : "Local resources";

    public string RemoveResourceLabel => IsChinese ? "移除" : "Remove";

    public string CurrentTargetLabel => IsChinese ? "当前实例" : "Current instance";

    public string CurrentTargetPathLabel => IsChinese ? "实例目录" : "Instance path";

    public string CurrentVersionSummaryLabel => IsChinese ? "运行链路" : "Runtime chain";

    public string ActivityLabel => IsChinese ? "下载状态" : "Activity";

    public string OpenInstallPanelLabel => IsChinese ? "选择安装方案" : "Choose install plan";

    public string InstallPanelTitle => IsChinese ? "三级安装面板" : "Third-level install panel";

    public string InstallPanelSummary => IsChinese
        ? "先确认目标版本，再决定是否给当前实例附加 Fabric / Forge / NeoForge。"
        : "Confirm the game version, then choose whether to attach Fabric, Forge, or NeoForge to this instance.";

    public string InstallPanelLoaderSourceLabel => IsChinese ? "加载器源" : "Loader source";

    public string ConfirmInstallLabel => IsChinese ? "开始安装" : "Start install";

    public string CancelInstallLabel => IsChinese ? "取消" : "Cancel";

    public string ResourceProviderSummary => IsChinese
        ? "资源下载仅保留 Modrinth，并实时读取当前实例目录。"
        : "Resource downloads now use Modrinth only and live-read the current instance folders.";

    public string ActiveSectionTitle => CurrentSection switch
    {
        DownloadSection.Games => IsChinese ? "先为实例准备游戏本体" : "Prepare the game runtime for an instance",
        DownloadSection.Loaders => IsChinese ? "单独缓存加载器安装器" : "Cache loader installers separately",
        DownloadSection.Resources => IsChinese ? "搜索并管理实例资源" : "Search and manage instance resources",
        _ => string.Empty,
    };

    public string ActiveSectionSummary => CurrentSection switch
    {
        DownloadSection.Games => IsChinese
            ? "版本选择被放到下方滚动带里，便于横向滑动浏览。"
            : "The version picker now sits in a lower horizontal strip for easier scrolling.",
        DownloadSection.Loaders => IsChinese
            ? "这里保留独立安装器下载，适合离线缓存 Fabric / Forge / NeoForge。"
            : "This tab keeps standalone installer downloads for offline caching.",
        DownloadSection.Resources => IsChinese
            ? "只保留 Modrinth 搜索，并实时管理当前实例的 mods、resourcepacks 和 shaderpacks。"
            : "Only Modrinth remains here, with live management of the current instance mods, resourcepacks, and shaderpacks.",
        _ => string.Empty,
    };

    public string CurrentInstanceName => SelectedInstance?.Name ?? (IsChinese ? launcherState.ActiveProfileNameZh : launcherState.ActiveProfileName);

    public string CurrentTargetPath => SelectedInstance is null
        ? launcherState.InstallPath
        : downloadCenterService.GetInstanceRootPath(launcherState.InstallPath, SelectedInstance.Id);

    public string CurrentInstanceVersionSummary => SelectedInstance?.VersionSummary ?? string.Empty;

    public string SelectedGameVersionDisplay => SelectedGameVersion?.Id ?? (IsChinese ? "未选择版本" : "No version selected");

    public string SelectedGameReleaseTimeDisplay => SelectedGameVersion?.ReleaseTime ?? string.Empty;

    public string ResourceContextSummary
    {
        get
        {
            var resourceLabel = SelectedResourceKind switch
            {
                ResourceContentType.Mod => IsChinese ? "Mod" : "Mod",
                ResourceContentType.ResourcePack => IsChinese ? "资源包" : "Resource pack",
                ResourceContentType.ShaderPack => IsChinese ? "光影" : "Shader",
                _ => string.Empty,
            };

            return IsChinese
                ? $"当前将把 {resourceLabel} 安装到 {CurrentInstanceName}。"
                : $"{resourceLabel} files will be installed into {CurrentInstanceName}.";
        }
    }

    public bool IsGamesSelected => CurrentSection == DownloadSection.Games;

    public bool IsLoadersSelected => CurrentSection == DownloadSection.Loaders;

    public bool IsResourcesSelected => CurrentSection == DownloadSection.Resources;

    public bool IsGameInstallLoaderVisible => SelectedGameInstallOption.RequiresLoaderVersion;

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool HasLocalResources => LocalResources.Count > 0;

    public bool IsBusy => IsRefreshingGames
                          || IsDownloadingGame
                          || IsRefreshingLoaders
                          || IsDownloadingLoader
                          || IsSearchingResources
                          || IsInstallingResource
                          || IsRefreshingLocalResources
                          || IsRemovingLocalResource;

    [ObservableProperty]
    private DownloadSection currentSection = DownloadSection.Games;

    [ObservableProperty]
    private DownloadSourceOption selectedGameSource;

    [ObservableProperty]
    private DownloadSourceOption selectedLoaderSource;

    [ObservableProperty]
    private IReadOnlyList<InstanceSelectionItem> availableInstances = [];

    [ObservableProperty]
    private InstanceSelectionItem? selectedInstance;

    [ObservableProperty]
    private IReadOnlyList<DownloadableGameVersion> availableGameVersions = [];

    [ObservableProperty]
    private DownloadableGameVersion? selectedGameVersion;

    [ObservableProperty]
    private GameInstallOption selectedGameInstallOption;

    [ObservableProperty]
    private bool isGameInstallPanelOpen;

    [ObservableProperty]
    private LoaderKind selectedLoaderKind = LoaderKind.Fabric;

    [ObservableProperty]
    private IReadOnlyList<DownloadableLoaderVersion> availableLoaderVersions = [];

    [ObservableProperty]
    private DownloadableLoaderVersion? selectedLoaderVersion;

    [ObservableProperty]
    private ResourceContentType selectedResourceKind = ResourceContentType.Mod;

    [ObservableProperty]
    private string resourceSearchQuery = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SearchableResourceResult> searchResults = [];

    [ObservableProperty]
    private IReadOnlyList<LocalResourceFileItem> localResources = [];

    [ObservableProperty]
    private bool isRefreshingGames;

    [ObservableProperty]
    private bool isDownloadingGame;

    [ObservableProperty]
    private bool isRefreshingLoaders;

    [ObservableProperty]
    private bool isDownloadingLoader;

    [ObservableProperty]
    private bool isSearchingResources;

    [ObservableProperty]
    private bool isInstallingResource;

    [ObservableProperty]
    private bool isRefreshingLocalResources;

    [ObservableProperty]
    private bool isRemovingLocalResource;

    [ObservableProperty]
    private double activityProgress;

    [ObservableProperty]
    private string activityMessage = string.Empty;

    [ObservableProperty]
    private string resourceStatusMessage = string.Empty;

    partial void OnIsRefreshingGamesChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsDownloadingGameChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsRefreshingLoadersChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsDownloadingLoaderChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsSearchingResourcesChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsInstallingResourceChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsRefreshingLocalResourcesChanged(bool value) => OnPropertyChanged(nameof(IsBusy));
    partial void OnIsRemovingLocalResourceChanged(bool value) => OnPropertyChanged(nameof(IsBusy));

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
        _ = RefreshLoaderVersionsCoreAsync();
    }

    partial void OnSelectedInstanceChanged(InstanceSelectionItem? value)
    {
        if (value is null)
        {
            return;
        }

        if (!suppressInstanceSelectionSync)
        {
            launcherState.ActivateVersion(value.Id);
        }

        RaiseProperties(nameof(CurrentInstanceName), nameof(CurrentTargetPath), nameof(CurrentInstanceVersionSummary),
            nameof(ResourceContextSummary));
        ConfigureLocalResourceWatchers();
        _ = RefreshLocalResourcesAsync();
    }

    partial void OnSelectedLoaderKindChanged(LoaderKind value)
    {
        _ = RefreshLoaderVersionsCoreAsync();
    }

    partial void OnSelectedGameVersionChanged(DownloadableGameVersion? value)
    {
        RaiseProperties(nameof(SelectedGameVersionDisplay), nameof(SelectedGameReleaseTimeDisplay));

        if (SelectedGameInstallOption.RequiresLoaderVersion)
        {
            _ = RefreshLoaderVersionsCoreAsync();
        }
    }

    partial void OnSelectedGameInstallOptionChanged(GameInstallOption value)
    {
        RaiseProperties(nameof(IsGameInstallLoaderVisible));

        if (value.LoaderKind is LoaderKind loaderKind)
        {
            SelectedLoaderKind = loaderKind;
            _ = RefreshLoaderVersionsCoreAsync();
        }
        else
        {
            AvailableLoaderVersions = [];
            SelectedLoaderVersion = null;
        }
    }

    partial void OnSelectedResourceKindChanged(ResourceContentType value)
    {
        SearchResults = [];
        ResourceStatusMessage = GetDefaultResourceStatus();
        RaiseProperties(nameof(ResourceContextSummary), nameof(HasSearchResults));
        _ = RefreshLocalResourcesAsync();
    }

    protected override void OnLanguageChanged()
    {
        RefreshInstances();
        SyncSelectedInstanceFromState();
        _ = RefreshLocalResourcesAsync();

        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SectionSummary),
            nameof(TabsLabel),
            nameof(GamesTabLabel),
            nameof(LoadersTabLabel),
            nameof(ResourcesTabLabel),
            nameof(SourceLabel),
            nameof(InstanceLabel),
            nameof(GameVersionLabel),
            nameof(RefreshLabel),
            nameof(DownloadGameLabel),
            nameof(LoaderKindLabel),
            nameof(LoaderVersionLabel),
            nameof(DownloadLoaderLabel),
            nameof(ResourceKindLabel),
            nameof(ResourceSearchLabel),
            nameof(ResourceSearchHint),
            nameof(SearchResourceLabel),
            nameof(InstallResourceLabel),
            nameof(SearchResultsLabel),
            nameof(LocalResourcesLabel),
            nameof(RemoveResourceLabel),
            nameof(CurrentTargetLabel),
            nameof(CurrentTargetPathLabel),
            nameof(CurrentVersionSummaryLabel),
            nameof(ActivityLabel),
            nameof(OpenInstallPanelLabel),
            nameof(InstallPanelTitle),
            nameof(InstallPanelSummary),
            nameof(InstallPanelLoaderSourceLabel),
            nameof(ConfirmInstallLabel),
            nameof(CancelInstallLabel),
            nameof(ResourceProviderSummary),
            nameof(ActiveSectionTitle),
            nameof(ActiveSectionSummary),
            nameof(CurrentInstanceName),
            nameof(CurrentTargetPath),
            nameof(CurrentInstanceVersionSummary),
            nameof(SelectedGameVersionDisplay),
            nameof(SelectedGameReleaseTimeDisplay),
            nameof(ResourceContextSummary));
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
        if (IsRefreshingGames || IsDownloadingGame)
        {
            return;
        }

        try
        {
            IsRefreshingGames = true;
            ActivityProgress = 5;
            ActivityMessage = IsChinese ? "正在刷新游戏版本列表..." : "Refreshing game version list...";
            await RefreshGameVersionsCoreAsync();
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
    private void OpenGameInstallPanel()
    {
        if (SelectedGameVersion is null)
        {
            ActivityProgress = 0;
            ActivityMessage = IsChinese ? "请先在下方选择一个游戏版本。" : "Choose a game version from the lower strip first.";
            return;
        }

        IsGameInstallPanelOpen = true;
        if (SelectedGameInstallOption.RequiresLoaderVersion)
        {
            _ = RefreshLoaderVersionsCoreAsync();
        }
    }

    [RelayCommand]
    private void CloseGameInstallPanel()
    {
        IsGameInstallPanelOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmGameInstallAsync()
    {
        if (SelectedGameVersion is null || SelectedInstance is null || IsDownloadingGame)
        {
            return;
        }

        if (SelectedGameInstallOption.RequiresLoaderVersion && SelectedLoaderVersion is null)
        {
            ActivityProgress = 0;
            ActivityMessage = IsChinese ? "请先选择加载器版本。" : "Select a loader version first.";
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

            var launchVersionId = SelectedGameVersion.Id;
            LoaderKind? loaderKind = null;
            string? loaderVersion = null;

            if (SelectedGameInstallOption.LoaderKind is LoaderKind selectedLoaderKind && SelectedLoaderVersion is not null)
            {
                loaderKind = selectedLoaderKind;
                loaderVersion = SelectedLoaderVersion.Version;
                ActivityProgress = 92;
                ActivityMessage = IsChinese ? "正在安装加载器到实例..." : "Installing loader into instance...";

                var installResult = await downloadCenterService.InstallLoaderAsync(
                    selectedLoaderKind,
                    SelectedLoaderSource.Source,
                    launcherState.InstallPath,
                    SelectedGameVersion.Id,
                    SelectedLoaderVersion,
                    launcherState.JavaRuntime);

                launchVersionId = installResult.ResolvedVersionId;
            }

            launcherState.UpdateInstanceRuntime(
                SelectedInstance.Id,
                SelectedGameVersion.Id,
                launchVersionId,
                loaderKind,
                loaderVersion);

            RefreshInstances();
            SyncSelectedInstanceFromState();
            await RefreshGameVersionsCoreAsync();
            await RefreshLocalResourcesAsync();

            ActivityProgress = 100;
            ActivityMessage = IsChinese
                ? $"已将 {SelectedGameVersion.Id} 安装到 {SelectedInstance.Name}"
                : $"{SelectedGameVersion.Id} was installed into {SelectedInstance.Name}";
            IsGameInstallPanelOpen = false;
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
        if (IsRefreshingLoaders || IsDownloadingLoader)
        {
            return;
        }

        try
        {
            IsRefreshingLoaders = true;
            ActivityProgress = 5;
            ActivityMessage = IsChinese ? "正在刷新加载器版本..." : "Refreshing loader versions...";
            await RefreshLoaderVersionsCoreAsync();
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
        if (SelectedLoaderVersion is null || IsDownloadingLoader)
        {
            return;
        }

        try
        {
            IsDownloadingLoader = true;
            ActivityProgress = 15;
            ActivityMessage = IsChinese ? "正在下载加载器安装器..." : "Downloading loader installer...";
            var targetPath = await downloadCenterService.DownloadLoaderAsync(
                SelectedLoaderKind,
                SelectedLoaderSource.Source,
                launcherState.InstallPath,
                SelectedLoaderVersion);
            ActivityProgress = 100;
            ActivityMessage = IsChinese
                ? $"安装器已保存到 {targetPath}"
                : $"Installer saved to {targetPath}";
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
    private async Task SearchResourcesAsync()
    {
        if (SelectedInstance is null || IsSearchingResources)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ResourceSearchQuery))
        {
            SearchResults = [];
            RaiseProperties(nameof(HasSearchResults));
            ResourceStatusMessage = GetDefaultResourceStatus();
            return;
        }

        try
        {
            IsSearchingResources = true;
            ResourceStatusMessage = IsChinese ? "正在从 Modrinth 搜索..." : "Searching Modrinth...";

            var searchResults = await downloadCenterService.SearchModrinthResourcesAsync(
                SelectedResourceKind,
                ResourceSearchQuery,
                launcherState.CurrentInstance.GetGameVersionId(),
                launcherState.CurrentInstance.GetLoaderKind());

            SearchResults = searchResults;
            RaiseProperties(nameof(HasSearchResults));

            ResourceStatusMessage = searchResults.Count == 0
                ? (IsChinese ? "没有找到匹配结果。" : "No matching results were found.")
                : (IsChinese ? $"找到 {searchResults.Count} 个结果。" : $"{searchResults.Count} result(s) found.");
        }
        catch (Exception ex)
        {
            SearchResults = [];
            RaiseProperties(nameof(HasSearchResults));
            ResourceStatusMessage = ex.Message;
        }
        finally
        {
            IsSearchingResources = false;
        }
    }

    [RelayCommand]
    private async Task InstallSearchResultAsync(SearchableResourceResult? result)
    {
        if (result is null || SelectedInstance is null || IsInstallingResource)
        {
            return;
        }

        try
        {
            IsInstallingResource = true;
            ActivityProgress = 15;
            ActivityMessage = IsChinese ? "正在安装资源到实例..." : "Installing resource into instance...";

            var targetPath = await downloadCenterService.DownloadResourceAsync(
                result,
                launcherState.InstallPath,
                SelectedInstance.Id);

            await RefreshLocalResourcesAsync();

            ActivityProgress = 100;
            ActivityMessage = IsChinese
                ? $"资源已安装到 {targetPath}"
                : $"Resource installed to {targetPath}";
            ResourceStatusMessage = IsChinese ? "实例资源列表已更新。" : "Instance resource list updated.";
        }
        catch (Exception ex)
        {
            ActivityProgress = 0;
            ActivityMessage = ex.Message;
            ResourceStatusMessage = ex.Message;
        }
        finally
        {
            IsInstallingResource = false;
        }
    }

    [RelayCommand]
    private async Task RefreshLocalResourcesAsync()
    {
        if (SelectedInstance is null)
        {
            LocalResources = [];
            RaiseProperties(nameof(HasLocalResources));
            return;
        }

        try
        {
            IsRefreshingLocalResources = true;
            LocalResources = downloadCenterService.GetLocalResources(
                launcherState.InstallPath,
                SelectedInstance.Id,
                SelectedResourceKind,
                IsChinese);
            RaiseProperties(nameof(HasLocalResources));

            if (LocalResources.Count == 0)
            {
                ResourceStatusMessage = IsChinese ? "当前实例目录里还没有这类资源。" : "This instance does not have local files of this type yet.";
            }
        }
        catch (Exception ex)
        {
            ResourceStatusMessage = ex.Message;
        }
        finally
        {
            IsRefreshingLocalResources = false;
        }
    }

    [RelayCommand]
    private async Task RemoveLocalResourceAsync(LocalResourceFileItem? resource)
    {
        if (resource is null || IsRemovingLocalResource)
        {
            return;
        }

        try
        {
            IsRemovingLocalResource = true;
            downloadCenterService.DeleteLocalResource(resource);
            await RefreshLocalResourcesAsync();
            ResourceStatusMessage = IsChinese ? "本地资源已移除。" : "Local resource removed.";
        }
        catch (Exception ex)
        {
            ResourceStatusMessage = ex.Message;
        }
        finally
        {
            IsRemovingLocalResource = false;
        }
    }

    private async Task RefreshGameVersionsCoreAsync()
    {
        AvailableGameVersions = await downloadCenterService.GetGameVersionsAsync(
            launcherState.InstallPath,
            SelectedGameSource.Source);

        SelectedGameVersion = AvailableGameVersions.FirstOrDefault(version =>
                                  SelectedGameVersion is not null
                                  && string.Equals(version.Id, SelectedGameVersion.Id, StringComparison.Ordinal))
                              ?? AvailableGameVersions.FirstOrDefault(version => !version.IsInstalled)
                              ?? AvailableGameVersions.FirstOrDefault();
    }

    private async Task RefreshLoaderVersionsCoreAsync()
    {
        var gameVersion = SelectedGameVersion?.Id ?? launcherState.CurrentInstance.GetGameVersionId();
        var loaderKind = SelectedGameInstallOption.LoaderKind ?? SelectedLoaderKind;

        AvailableLoaderVersions = await downloadCenterService.GetLoaderVersionsAsync(
            loaderKind,
            SelectedLoaderSource.Source,
            gameVersion);

        SelectedLoaderVersion = AvailableLoaderVersions.FirstOrDefault(version =>
                                   SelectedLoaderVersion is not null
                                   && string.Equals(version.Version, SelectedLoaderVersion.Version, StringComparison.Ordinal))
                               ?? AvailableLoaderVersions.FirstOrDefault();
    }

    private void RefreshInstances()
    {
        AvailableInstances = launcherState.GetLocalizedInstanceSelections(IsChinese);
    }

    private void SyncSelectedInstanceFromState()
    {
        suppressInstanceSelectionSync = true;
        SelectedInstance = AvailableInstances.FirstOrDefault(instance => instance.Id == launcherState.CurrentVersionId)
                           ?? AvailableInstances.FirstOrDefault();
        suppressInstanceSelectionSync = false;
    }

    private string GetDefaultResourceStatus()
    {
        return IsChinese ? "输入关键字后从 Modrinth 搜索资源。" : "Enter a keyword to search Modrinth resources.";
    }

    private void ConfigureLocalResourceWatchers()
    {
        DisposeWatchers();

        if (SelectedInstance is null)
        {
            return;
        }

        modsWatcher = CreateWatcher(downloadCenterService.GetResourceDirectory(
            launcherState.InstallPath,
            SelectedInstance.Id,
            ResourceContentType.Mod));

        resourcePackWatcher = CreateWatcher(downloadCenterService.GetResourceDirectory(
            launcherState.InstallPath,
            SelectedInstance.Id,
            ResourceContentType.ResourcePack));

        shaderWatcher = CreateWatcher(downloadCenterService.GetResourceDirectory(
            launcherState.InstallPath,
            SelectedInstance.Id,
            ResourceContentType.ShaderPack));
    }

    private FileSystemWatcher CreateWatcher(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        var watcher = new FileSystemWatcher(directoryPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        watcher.Created += HandleResourceDirectoryChanged;
        watcher.Changed += HandleResourceDirectoryChanged;
        watcher.Deleted += HandleResourceDirectoryChanged;
        watcher.Renamed += HandleResourceDirectoryChanged;
        return watcher;
    }

    private void DisposeWatchers()
    {
        DisposeWatcher(modsWatcher);
        DisposeWatcher(resourcePackWatcher);
        DisposeWatcher(shaderWatcher);
        modsWatcher = null;
        resourcePackWatcher = null;
        shaderWatcher = null;
    }

    private static void DisposeWatcher(FileSystemWatcher? watcher)
    {
        if (watcher is null)
        {
            return;
        }

        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    private void HandleResourceDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() => _ = RefreshLocalResourcesAsync());
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherStateService.CurrentVersionId) or nameof(LauncherStateService.InstallPath) or nameof(LauncherStateService.Instances))
        {
            RefreshInstances();
            SyncSelectedInstanceFromState();
            ConfigureLocalResourceWatchers();
            _ = RefreshLocalResourcesAsync();
            RaiseProperties(nameof(CurrentInstanceName), nameof(CurrentTargetPath), nameof(CurrentInstanceVersionSummary),
                nameof(ResourceContextSummary));
        }

        if (e.PropertyName == nameof(LauncherStateService.InstallPath))
        {
            _ = RefreshGameVersionsAsync();
            _ = RefreshLoaderVersionsCoreAsync();
        }
    }
}
