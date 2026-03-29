using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLauncher.InstanceManger;
using AZLauncher.Models;
using AZLauncher.ModManager;

namespace AZLauncher.Services;

public sealed partial class LauncherStateService : ObservableObject
{
    private readonly AppConfigService configService;
    private readonly VersionIsolationService versionIsolationService;
    private readonly MinecraftRuntimeService minecraftRuntimeService;
    private readonly List<Instance> instances;
    private readonly IReadOnlyList<ResourcePack> resourcePacks;
    private readonly IReadOnlyList<ShaderPack> shaderPacks;

    public LauncherStateService(AppConfigService configService)
    {
        this.configService = configService;
        versionIsolationService = new VersionIsolationService();
        minecraftRuntimeService = new MinecraftRuntimeService();
        this.configService.PropertyChanged += HandleConfigPropertyChanged;

        instances =
        [
            BuildFabricInstance(),
            BuildNeoForgeInstance(),
            BuildSnapshotInstance(),
        ];

        resourcePacks =
        [
            new ResourcePack(
                id: "faithful-32x",
                nick: "Faithful 32x",
                nickZh: "Faithful 32x",
                description: "High-resolution pack that preserves the vanilla look and can be toggled per instance.",
                descriptionZh: "保留原版风格的高清材质包，可按实例开关。",
                category: "Resource Pack",
                categoryZh: "资源包",
                isEnabled: true),
            new ResourcePack(
                id: "builder-ui-clean",
                nick: "Builder UI Clean",
                nickZh: "Builder UI Clean",
                description: "A lighter UI resource profile for building sessions with less HUD clutter.",
                descriptionZh: "针对建筑档优化的轻量界面资源，减少 HUD 干扰。",
                category: "Interface Skin",
                categoryZh: "界面资源",
                isEnabled: false),
        ];

        shaderPacks =
        [
            new ShaderPack(
                id: "complementary-reimagined",
                nick: "Complementary Reimagined",
                nickZh: "Complementary Reimagined",
                description: "Balanced daytime lighting and strong compatibility for everyday survival worlds.",
                descriptionZh: "日间光照均衡、兼容性稳定，适合长期生存档。",
                category: "Balanced",
                categoryZh: "均衡",
                isEnabled: true),
            new ShaderPack(
                id: "bsl-cinematic",
                nick: "BSL Cinematic",
                nickZh: "BSL Cinematic",
                description: "Heavier cinematic preset tuned for screenshots, sunsets, and showcase builds.",
                descriptionZh: "偏重截图和展示建筑的电影感光影配置。",
                category: "Cinematic",
                categoryZh: "电影感",
                isEnabled: false),
        ];

        ScreenshotCount = 186;
        FriendsOnline = 7;
        CurrentVersionId = instances[0].GetId();

        DetectInstalledVersions();
        RefreshDerivedState();
    }

    [ObservableProperty]
    private int screenshotCount;

    [ObservableProperty]
    private int friendsOnline;

    [ObservableProperty]
    private string currentVersionId = string.Empty;

    [ObservableProperty]
    private long availableStorageBytes;

    [ObservableProperty]
    private int installedBuildCount;

    [ObservableProperty]
    private int modCount;

    [ObservableProperty]
    private IReadOnlyList<DownloadableGameVersion> availableGameVersions = [];

    [ObservableProperty]
    private DownloadableGameVersion? selectedDownloadVersion;

    [ObservableProperty]
    private bool isRefreshingGameVersions;

    [ObservableProperty]
    private bool isDownloadingGame;

    [ObservableProperty]
    private bool isLaunchingGame;

    [ObservableProperty]
    private double operationProgress;

    [ObservableProperty]
    private string operationDetail = string.Empty;

    [ObservableProperty]
    private string lastLaunchCommandPreview = string.Empty;

    public IReadOnlyList<Instance> Instances => instances;

    public IReadOnlyList<string> InstanceFolders => configService.InstanceFolders;

    public string InstallPath => configService.ActiveInstanceFolder;

    public string JavaRuntime => configService.DefaultJavaRuntime;

    public int AllocatedMemoryGb => configService.DefaultMemoryGb;

    public Instance CurrentInstance =>
        instances.FirstOrDefault(instance => instance.GetId() == CurrentVersionId) ?? instances[0];

    public string ActiveProfileName => CurrentInstance.GetNick();

    public string ActiveProfileNameZh => CurrentInstance.GetNickZh();

    public string LaunchState => "Ready with V3 isolation";

    public string LaunchStateZh => "已启用第三代隔离，可启动";

    public string VersionIsolationRoot => versionIsolationService.IsolationRoot;

    public string VersionIsolationSummary => "Shared store by resource ID, symlink first, copy fallback";

    public string VersionIsolationSummaryZh => "按资源 ID 共享存储，软链接优先，不支持时实时复制";

    public bool IsBusy => IsRefreshingGameVersions || IsDownloadingGame || IsLaunchingGame;

    partial void OnIsRefreshingGameVersionsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    partial void OnIsDownloadingGameChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    partial void OnIsLaunchingGameChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    partial void OnCurrentVersionIdChanged(string value)
    {
        RefreshDerivedState();
        OnPropertyChanged(nameof(CurrentInstance));
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ActiveProfileNameZh));
    }

    public void ActivateVersion(string versionId)
    {
        var instance = instances.FirstOrDefault(item => item.GetId() == versionId);
        if (instance is null)
        {
            return;
        }

        if (instance.GetIsDetected()
            && !string.IsNullOrWhiteSpace(instance.GetDetectedRoot())
            && !string.Equals(configService.ActiveInstanceFolder, instance.GetDetectedRoot(), StringComparison.OrdinalIgnoreCase))
        {
            configService.ActiveInstanceFolder = instance.GetDetectedRoot();
        }

        CurrentVersionId = versionId;
    }

    public int DetectInstalledVersions()
    {
        var previousInstance = instances.FirstOrDefault(instance => instance.GetId() == CurrentVersionId);
        var previousLaunchVersionId = previousInstance?.GetLaunchVersionId();

        instances.RemoveAll(instance => instance.GetIsDetected());

        var detectedInstances = GetAllScanRoots()
            .SelectMany(ScanInstalledVersions)
            .OrderBy(instance => instance.GetLaunchVersionId(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(instance => instance.GetDetectedRoot(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var detectedInstance in detectedInstances)
        {
            instances.Add(detectedInstance);
        }

        if (!instances.Any(instance => instance.GetId() == CurrentVersionId))
        {
            var replacement = !string.IsNullOrWhiteSpace(previousLaunchVersionId)
                ? instances.FirstOrDefault(instance =>
                    string.Equals(instance.GetLaunchVersionId(), previousLaunchVersionId, StringComparison.OrdinalIgnoreCase))
                : null;

            CurrentVersionId = replacement?.GetId() ?? instances.First().GetId();
        }

        RefreshDerivedState();
        OnPropertyChanged(nameof(Instances));
        OnPropertyChanged(nameof(CurrentInstance));
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ActiveProfileNameZh));
        return detectedInstances.Count;
    }

    public async Task RefreshAvailableGameVersionsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsRefreshingGameVersions = true;
            OperationProgress = 0;
            OperationDetail = "Refreshing official version list...";

            var previousSelectedId = SelectedDownloadVersion?.Id;
            AvailableGameVersions = await minecraftRuntimeService.GetAvailableVersionsAsync(InstallPath, DownloadSource.Official, cancellationToken);
            SelectedDownloadVersion = AvailableGameVersions.FirstOrDefault(version =>
                                      string.Equals(version.Id, previousSelectedId, StringComparison.Ordinal))
                                  ?? AvailableGameVersions.FirstOrDefault(version => !version.IsInstalled)
                                  ?? AvailableGameVersions.FirstOrDefault();
            OperationProgress = 100;
            OperationDetail = $"Loaded {AvailableGameVersions.Count} official versions.";
        }
        catch (Exception ex)
        {
            OperationDetail = ex.Message;
        }
        finally
        {
            IsRefreshingGameVersions = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task DownloadSelectedGameAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || SelectedDownloadVersion is null)
        {
            return;
        }

        try
        {
            IsDownloadingGame = true;
            OperationProgress = 0;
            OperationDetail = $"Downloading {SelectedDownloadVersion.Id}...";

            var progress = new Progress<RuntimeProgressUpdate>(update =>
            {
                OperationProgress = update.Progress;
                OperationDetail = update.Message;
            });

            await minecraftRuntimeService.DownloadVersionAsync(
                SelectedDownloadVersion.Id,
                InstallPath,
                DownloadSource.Official,
                progress,
                cancellationToken);
            AddDownloadedInstance(SelectedDownloadVersion);
            await RefreshAvailableGameVersionsAsync(cancellationToken);
            OperationDetail = $"Downloaded {SelectedDownloadVersion.Id} successfully.";
        }
        catch (Exception ex)
        {
            OperationDetail = ex.Message;
        }
        finally
        {
            IsDownloadingGame = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task LaunchCurrentGameAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsLaunchingGame = true;
            OperationProgress = 10;
            LastLaunchCommandPreview = string.Empty;

            var gameVersionId = CurrentInstance.GetLaunchVersionId();
            if (!minecraftRuntimeService.IsVersionInstalled(InstallPath, gameVersionId))
            {
                OperationDetail = $"Version {gameVersionId} is not installed. Download it first.";
                OperationProgress = 0;
                return;
            }

            OperationDetail = $"Preparing isolation for {CurrentInstance.GetNick()}...";
            var launchResources = BuildLaunchResources(CurrentInstance);
            var isolation = await PrepareInstanceIsolationAsync(CurrentInstance.GetId(), launchResources, cancellationToken);
            OperationProgress = 45;

            var result = await minecraftRuntimeService.LaunchAsync(
                gameVersionId,
                InstallPath,
                isolation.InstanceRoot,
                JavaRuntime,
                AllocatedMemoryGb,
                configService.ActiveUserName,
                configService.LauncherTitle,
                GetLauncherVersionLabel(),
                cancellationToken);

            LastLaunchCommandPreview = result.CommandPreview;
            OperationDetail = result.Message;
            OperationProgress = result.Started ? 100 : 0;
        }
        catch (Exception ex)
        {
            OperationDetail = ex.Message;
            OperationProgress = 0;
        }
        finally
        {
            IsLaunchingGame = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public Task<VersionIsolationResult> PrepareInstanceIsolationAsync(
        string instanceId,
        IEnumerable<VersionIsolationDescriptor> resources,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var targetInstanceId = string.IsNullOrWhiteSpace(instanceId) ? CurrentInstance.GetId() : instanceId;
        var instanceRoot = Path.Combine(InstallPath, "instances", targetInstanceId);
        return versionIsolationService.PrepareInstanceAsync(targetInstanceId, instanceRoot, resources, cancellationToken);
    }

    private IReadOnlyList<VersionIsolationDescriptor> BuildLaunchResources(Instance instance)
    {
        var resourceRoots = GetLaunchResourceRoots(instance).ToArray();
        if (resourceRoots.Length == 0)
        {
            return [];
        }

        var descriptorMap = new Dictionary<string, VersionIsolationDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceRoot in resourceRoots)
        {
            foreach (var directoryName in GetLaunchResourceDirectories())
            {
                var sourceDirectory = Path.Combine(resourceRoot, directoryName);
                if (!Directory.Exists(sourceDirectory))
                {
                    continue;
                }

                foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        continue;
                    }

                    var targetSubdirectory = Path.Combine(directoryName, Path.GetDirectoryName(relativePath) ?? string.Empty)
                        .TrimEnd(Path.DirectorySeparatorChar);
                    var targetFileName = Path.GetFileName(filePath);
                    var targetKey = $"{targetSubdirectory}|{targetFileName}";
                    descriptorMap[targetKey] = new VersionIsolationDescriptor
                    {
                        ResourceId = BuildIsolationResourceId(filePath),
                        SourceFilePath = filePath,
                        TargetSubdirectory = targetSubdirectory,
                        TargetFileName = targetFileName,
                    };
                }
            }

            foreach (var fileName in GetLaunchRootFiles())
            {
                var sourcePath = Path.Combine(resourceRoot, fileName);
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                descriptorMap[fileName] = new VersionIsolationDescriptor
                {
                    ResourceId = BuildIsolationResourceId(sourcePath),
                    SourceFilePath = sourcePath,
                    TargetSubdirectory = string.Empty,
                    TargetFileName = fileName,
                };
            }
        }

        return descriptorMap.Values.ToArray();
    }

    public void RefreshStorageInfo()
    {
        try
        {
            var targetPath = string.IsNullOrWhiteSpace(InstallPath) ? AppContext.BaseDirectory : InstallPath;
            var fullPath = Path.GetFullPath(targetPath);
            var root = Path.GetPathRoot(fullPath);

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetPathRoot(AppContext.BaseDirectory);
            }

            AvailableStorageBytes = string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            AvailableStorageBytes = 0;
        }
    }

    public string FormatStorage(bool isChinese)
    {
        if (AvailableStorageBytes <= 0)
        {
            return isChinese ? "无法获取" : "Unavailable";
        }

        return FormatBytes(AvailableStorageBytes, isChinese);
    }

    public string FormatMemory(bool isChinese)
    {
        return isChinese ? $"已分配 {AllocatedMemoryGb} GB" : $"{AllocatedMemoryGb} GB allocated";
    }

    public IReadOnlyList<LauncherVersion> GetLocalizedVersions(bool isChinese)
    {
        return instances
            .Select(instance => new LauncherVersion
            {
                Id = instance.GetId(),
                Name = isChinese ? instance.GetNickZh() : instance.GetNick(),
                Channel = isChinese ? instance.GetChannelZh() : instance.GetChannel(),
                Summary = isChinese ? instance.GetSummaryZh() : instance.GetSummary(),
                LastPlayed = isChinese ? instance.GetLastPlayedZh() : instance.GetLastPlayed(),
                IsRecommended = instance.GetRecommended(),
                IsActive = instance.GetId() == CurrentVersionId,
                HasBadge = instance.GetId() == CurrentVersionId || instance.GetRecommended(),
                BadgeText = instance.GetId() == CurrentVersionId
                    ? (isChinese ? "当前" : "Active")
                    : instance.GetRecommended()
                        ? (isChinese ? "推荐" : "Recommended")
                        : null,
            })
            .ToArray();
    }

    public IReadOnlyList<InstanceSelectionItem> GetLocalizedInstanceSelections(bool isChinese)
    {
        return instances
            .Select(instance => new InstanceSelectionItem
            {
                Id = instance.GetId(),
                Name = isChinese ? instance.GetNickZh() : instance.GetNick(),
                VersionSummary = BuildInstanceVersionSummary(instance, isChinese),
                Summary = isChinese ? instance.GetSummaryZh() : instance.GetSummary(),
                SourcePath = instance.GetIsDetected() && !string.IsNullOrWhiteSpace(instance.GetDetectedRoot())
                    ? instance.GetDetectedRoot()
                    : InstallPath,
                IsActive = instance.GetId() == CurrentVersionId,
            })
            .ToArray();
    }

    public IReadOnlyList<LibraryItem> GetLocalizedMods(bool isChinese)
    {
        return CurrentInstance.GetMods()
            .Select(mod => new LibraryItem
            {
                Name = isChinese ? mod.GetNickZh() : mod.GetNick(),
                Category = isChinese ? mod.GetCategoryZh() : mod.GetCategory(),
                State = mod.GetEnabled() ? (isChinese ? "已启用" : "Enabled") : (isChinese ? "已禁用" : "Disabled"),
                Summary = isChinese ? mod.GetDescriptionZh() : mod.GetDescription(),
                Kind = LibraryItemKind.Mod,
            })
            .ToArray();
    }

    public IReadOnlyList<LibraryItem> GetLocalizedResourcePacks(bool isChinese)
    {
        return resourcePacks
            .Select(resourcePack => new LibraryItem
            {
                Name = isChinese ? resourcePack.GetNickZh() : resourcePack.GetNick(),
                Category = isChinese ? resourcePack.GetCategoryZh() : resourcePack.GetCategory(),
                State = resourcePack.GetEnabled()
                    ? (isChinese ? "已启用" : "Enabled")
                    : (isChinese ? "待启用" : "Standby"),
                Summary = isChinese ? resourcePack.GetDescriptionZh() : resourcePack.GetDescription(),
                Kind = LibraryItemKind.ResourcePack,
            })
            .ToArray();
    }

    public IReadOnlyList<LibraryItem> GetLocalizedShaders(bool isChinese)
    {
        return shaderPacks
            .Select(shaderPack => new LibraryItem
            {
                Name = isChinese ? shaderPack.GetNickZh() : shaderPack.GetNick(),
                Category = isChinese ? shaderPack.GetCategoryZh() : shaderPack.GetCategory(),
                State = shaderPack.GetEnabled()
                    ? (isChinese ? "已启用" : "Enabled")
                    : (isChinese ? "待切换" : "Standby"),
                Summary = isChinese ? shaderPack.GetDescriptionZh() : shaderPack.GetDescription(),
                Kind = LibraryItemKind.Shader,
            })
            .ToArray();
    }

    private void RefreshDerivedState()
    {
        InstalledBuildCount = instances.Count;
        ModCount = CurrentInstance.GetEnabledModCount();
        RefreshStorageInfo();
    }

    public void UpdateInstanceRuntime(
        string instanceId,
        string gameVersionId,
        string launchVersionId,
        LoaderKind? loaderKind,
        string? loaderVersion)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(gameVersionId) || string.IsNullOrWhiteSpace(launchVersionId))
        {
            return;
        }

        var instance = instances.FirstOrDefault(item => string.Equals(item.GetId(), instanceId, StringComparison.Ordinal));
        if (instance is null)
        {
            instance = new Instance(
                nick: instanceId,
                nickZh: instanceId,
                id: instanceId,
                gameVersionId: gameVersionId,
                summary: $"Runtime chain prepared for {launchVersionId}.",
                summaryZh: $"已为 {launchVersionId} 准备运行链路。",
                channel: loaderKind?.ToString() ?? "Vanilla",
                channelZh: loaderKind switch
                {
                    LoaderKind.Fabric => "Fabric",
                    LoaderKind.Forge => "Forge",
                    LoaderKind.NeoForge => "NeoForge",
                    _ => "原版",
                },
                lastPlayed: "Not launched yet",
                lastPlayedZh: "尚未启动",
                isRecommended: false,
                launchVersionId: launchVersionId,
                loaderKind: loaderKind,
                loaderVersion: loaderVersion);

            instances.Add(instance);
            OnPropertyChanged(nameof(Instances));
        }
        else
        {
            instance.SetGameVersionId(gameVersionId);
            instance.SetLaunchVersionId(launchVersionId);
            instance.SetLoader(loaderKind, loaderVersion);
            instance.SetSummary(
                BuildRuntimeSummary(instance.GetNick(), gameVersionId, launchVersionId, loaderKind, loaderVersion, false),
                BuildRuntimeSummary(instance.GetNickZh(), gameVersionId, launchVersionId, loaderKind, loaderVersion, true));
            instance.SetChannel(
                loaderKind?.ToString() ?? "Vanilla",
                loaderKind switch
                {
                    LoaderKind.Fabric => "Fabric",
                    LoaderKind.Forge => "Forge",
                    LoaderKind.NeoForge => "NeoForge",
                    _ => "原版",
                });
        }

        CurrentVersionId = instance.GetId();
        RefreshDerivedState();
        OnPropertyChanged(nameof(CurrentInstance));
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ActiveProfileNameZh));
        OnPropertyChanged(nameof(Instances));
    }

    private static Instance BuildFabricInstance()
    {
        var instance = new Instance(
            nick: "Fabric 1.21.4",
            nickZh: "Fabric 1.21.4",
            id: "fabric-survival",
            gameVersionId: "1.21.4",
            summary: "Fast survival profile for daily play and shader testing.",
            summaryZh: "轻量生存配置，适合日常游玩和夜间光影测试。",
            channel: "Primary",
            channelZh: "主力",
            lastPlayed: "Played 2 hours ago",
            lastPlayedZh: "2 小时前游玩",
            isRecommended: true,
            launchVersionId: "1.21.4",
            loaderKind: LoaderKind.Fabric,
            loaderVersion: "0.16.x");

        instance.AddMod(new Mod(
            id: "sodium",
            nick: "Sodium",
            nickZh: "Sodium",
            description: "High-performance rendering optimization for smooth chunk and lighting updates.",
            descriptionZh: "高性能渲染优化模组，用于提升区块与光照刷新效率。",
            category: "Performance",
            categoryZh: "性能",
            isEnabled: true));
        instance.AddMod(new Mod(
            id: "iris",
            nick: "Iris",
            nickZh: "Iris",
            description: "Shader pipeline integration for Fabric-based clients.",
            descriptionZh: "为 Fabric 客户端提供光影管线支持。",
            category: "Shaders",
            categoryZh: "光影",
            isEnabled: true));
        instance.AddMod(new Mod(
            id: "xaeros-map",
            nick: "Xaero's Minimap",
            nickZh: "Xaero 小地图",
            description: "Compact minimap with cave mode and waypoint support.",
            descriptionZh: "带洞穴视图和路径点支持的小地图模组。",
            category: "Utility",
            categoryZh: "工具",
            isEnabled: true));

        return instance;
    }

    private static Instance BuildNeoForgeInstance()
    {
        var instance = new Instance(
            nick: "NeoForge 1.20.1",
            nickZh: "NeoForge 1.20.1",
            id: "neoforge-tech",
            gameVersionId: "1.20.1",
            summary: "Heavy automation stack with expanded world generation and controller support.",
            summaryZh: "偏重工业和自动化的大型模组组合，带额外地形与手柄支持。",
            channel: "Modded",
            channelZh: "整合",
            lastPlayed: "Played yesterday",
            lastPlayedZh: "昨天游玩",
            isRecommended: false,
            launchVersionId: "1.20.1",
            loaderKind: LoaderKind.NeoForge,
            loaderVersion: "21.x");

        instance.AddMod(new Mod(
            id: "create",
            nick: "Create",
            nickZh: "Create",
            description: "Mechanical automation core for factories, belts, and kinetic builds.",
            descriptionZh: "机械自动化核心模组，适合工厂、传送带和动力结构。",
            category: "Automation",
            categoryZh: "工业",
            isEnabled: true));
        instance.AddMod(new Mod(
            id: "jei",
            nick: "JEI",
            nickZh: "JEI",
            description: "Recipe browser for large crafting trees and modded progression.",
            descriptionZh: "大型合成链和模组流程常用的配方查看器。",
            category: "Utility",
            categoryZh: "工具",
            isEnabled: true));
        instance.AddMod(new Mod(
            id: "worldgen-extra",
            nick: "Worldgen Extra",
            nickZh: "拓展地形生成",
            description: "Additional terrain layers and biome flavor for long-running worlds.",
            descriptionZh: "为长期游玩世界提供更多地形层次和群系变化。",
            category: "Worldgen",
            categoryZh: "地形",
            isEnabled: false));

        return instance;
    }

    private static Instance BuildSnapshotInstance()
    {
        var instance = new Instance(
            nick: "Snapshot 25w13a",
            nickZh: "Snapshot 25w13a",
            id: "snapshot-lab",
            gameVersionId: "25w13a",
            summary: "Disposable testing instance for preview features and config checks.",
            summaryZh: "用于测试新特性的快照实例，适合单独隔离试玩。",
            channel: "Experimental",
            channelZh: "实验",
            lastPlayed: "Played 4 days ago",
            lastPlayedZh: "4 天前游玩",
            isRecommended: false,
            launchVersionId: "25w13a");

        instance.AddMod(new Mod(
            id: "debug-tools",
            nick: "Debug Tools",
            nickZh: "调试工具",
            description: "Temporary helpers for snapshot validation and config smoke tests.",
            descriptionZh: "用于快照验证和配置冒烟测试的临时工具集合。",
            category: "Testing",
            categoryZh: "测试",
            isEnabled: true));
        instance.AddMod(new Mod(
            id: "ui-probe",
            nick: "UI Probe",
            nickZh: "界面探针",
            description: "Small diagnostics overlay for feature and HUD regression checks.",
            descriptionZh: "用于功能和 HUD 回归检查的小型诊断覆盖层。",
            category: "Diagnostics",
            categoryZh: "诊断",
            isEnabled: false));

        return instance;
    }

    private static string FormatBytes(long bytes, bool isChinese)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var number = size >= 100
            ? size.ToString("F0", CultureInfo.InvariantCulture)
            : size.ToString("F1", CultureInfo.InvariantCulture);

        return isChinese ? $"{number} {units[unitIndex]} 可用" : $"{number} {units[unitIndex]} free";
    }

    private void HandleConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfigService.ActiveInstanceFolder) or nameof(AppConfigService.InstanceFolders))
        {
            RefreshStorageInfo();
            DetectInstalledVersions();
            OnPropertyChanged(nameof(InstallPath));
            OnPropertyChanged(nameof(InstanceFolders));
            AvailableGameVersions = [];
            SelectedDownloadVersion = null;
        }

        if (e.PropertyName == nameof(AppConfigService.DefaultJavaRuntime))
        {
            OnPropertyChanged(nameof(JavaRuntime));
        }

        if (e.PropertyName == nameof(AppConfigService.DefaultMemoryGb))
        {
            OnPropertyChanged(nameof(AllocatedMemoryGb));
        }
    }

    private void AddDownloadedInstance(DownloadableGameVersion version)
    {
        DetectInstalledVersions();
        var detectedMatch = instances.FirstOrDefault(instance =>
            instance.GetIsDetected()
            && string.Equals(instance.GetDetectedRoot(), InstallPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(instance.GetLaunchVersionId(), version.Id, StringComparison.OrdinalIgnoreCase));

        if (detectedMatch is not null)
        {
            CurrentVersionId = detectedMatch.GetId();
            RefreshDerivedState();
            return;
        }

        var downloadedInstance = new Instance(
            nick: version.Id,
            nickZh: version.Id,
            id: version.Id,
            gameVersionId: version.Id,
            summary: $"Official {version.Type} runtime downloaded into the local launcher directory.",
            summaryZh: $"官方 {version.Type} 版本文件已下载到本地启动器目录。",
            channel: version.Type,
            channelZh: version.Type,
            lastPlayed: "Not launched yet",
            lastPlayedZh: "尚未启动",
            isRecommended: false,
            launchVersionId: version.Id);

        instances.Add(downloadedInstance);
        CurrentVersionId = downloadedInstance.GetId();
        RefreshDerivedState();
        OnPropertyChanged(nameof(Instances));
    }

    private IEnumerable<string> GetAllScanRoots()
    {
        return configService.InstanceFolders
            .Append(configService.ActiveInstanceFolder)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private List<Instance> ScanInstalledVersions(string installRoot)
    {
        var versionsRoot = Path.Combine(installRoot, "versions");
        if (!Directory.Exists(versionsRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(versionsRoot)
            .Select(directory => TryBuildDetectedInstance(directory, installRoot))
            .Where(instance => instance is not null)
            .Cast<Instance>()
            .OrderBy(instance => instance.GetLaunchVersionId(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Instance? TryBuildDetectedInstance(string versionDirectory, string installRoot)
    {
        try
        {
            var directoryName = Path.GetFileName(versionDirectory);
            var preferredJsonPath = Path.Combine(versionDirectory, $"{directoryName}.json");
            var jsonPath = File.Exists(preferredJsonPath)
                ? preferredJsonPath
                : Directory.EnumerateFiles(versionDirectory, "*.json").FirstOrDefault();

            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = document.RootElement;

            var launchVersionId = GetJsonString(root, "id") ?? directoryName;
            var inheritsFrom = GetJsonString(root, "inheritsFrom");
            var type = GetJsonString(root, "type") ?? "custom";
            var mainClass = GetJsonString(root, "mainClass");

            var (loaderKind, loaderVersion) = DetectLoader(root, launchVersionId, mainClass);
            var gameVersionId = ResolveGameVersionId(root, launchVersionId, inheritsFrom, loaderKind, loaderVersion);

            var displayName = loaderKind switch
            {
                LoaderKind.Fabric => $"Fabric {gameVersionId}",
                LoaderKind.Forge => $"Forge {gameVersionId}",
                LoaderKind.NeoForge => $"NeoForge {gameVersionId}",
                _ => launchVersionId,
            };

            var detectedId = BuildDetectedInstanceId(installRoot, launchVersionId);
            return new Instance(
                nick: displayName,
                nickZh: displayName,
                id: detectedId,
                gameVersionId: gameVersionId,
                summary: $"Detected from {installRoot} and compatible with other launchers using the standard .minecraft layout.",
                summaryZh: $"已从 {installRoot} 探测到该版本，兼容使用标准 .minecraft 结构的其他启动器。",
                channel: BuildDetectedChannel(type, loaderKind, false),
                channelZh: BuildDetectedChannel(type, loaderKind, true),
                lastPlayed: "Detected locally",
                lastPlayedZh: "已从本地探测",
                isRecommended: false,
                launchVersionId: launchVersionId,
                loaderKind: loaderKind,
                loaderVersion: loaderVersion,
                isDetected: true,
                detectedRoot: installRoot);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetJsonString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static (LoaderKind? LoaderKind, string LoaderVersion) DetectLoader(
        JsonElement root,
        string launchVersionId,
        string? mainClass)
    {
        if (TryFindLibraryVersion(root, "net.fabricmc:fabric-loader:", out var fabricVersion))
        {
            return (LoaderKind.Fabric, fabricVersion);
        }

        if (TryFindLibraryVersion(root, "net.neoforged:neoforge:", out var neoForgeVersion))
        {
            return (LoaderKind.NeoForge, neoForgeVersion);
        }

        if (TryFindLibraryVersion(root, "net.minecraftforge:forge:", out var forgeVersion))
        {
            return (LoaderKind.Forge, forgeVersion);
        }

        if (!string.IsNullOrWhiteSpace(mainClass) && mainClass.Contains("fabricmc", StringComparison.OrdinalIgnoreCase))
        {
            return (LoaderKind.Fabric, string.Empty);
        }

        if (launchVersionId.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            return (LoaderKind.NeoForge, string.Empty);
        }

        if (launchVersionId.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            return (LoaderKind.Forge, string.Empty);
        }

        return (null, string.Empty);
    }

    private static bool TryFindLibraryVersion(JsonElement root, string prefix, out string version)
    {
        version = string.Empty;
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!library.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString() ?? string.Empty;
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            version = name[prefix.Length..];
            return true;
        }

        return false;
    }

    private static string ResolveGameVersionId(
        JsonElement root,
        string launchVersionId,
        string? inheritsFrom,
        LoaderKind? loaderKind,
        string loaderVersion)
    {
        if (!string.IsNullOrWhiteSpace(inheritsFrom))
        {
            return inheritsFrom;
        }

        if (loaderKind is LoaderKind.Forge or LoaderKind.NeoForge)
        {
            var split = loaderVersion.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0 && !string.IsNullOrWhiteSpace(split[0]))
            {
                return split[0];
            }
        }

        if (loaderKind == LoaderKind.Fabric)
        {
            var parts = launchVersionId.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var loaderIndex = Array.FindIndex(parts, part => string.Equals(part, "loader", StringComparison.OrdinalIgnoreCase));
            if (loaderIndex >= 0 && loaderIndex < parts.Length - 2)
            {
                return parts[^1];
            }
        }

        if (root.TryGetProperty("jar", out var jarElement) && jarElement.ValueKind == JsonValueKind.String)
        {
            var jar = jarElement.GetString();
            if (!string.IsNullOrWhiteSpace(jar))
            {
                return jar!;
            }
        }

        return launchVersionId;
    }

    private static string BuildDetectedChannel(string type, LoaderKind? loaderKind, bool isChinese)
    {
        if (loaderKind is not null)
        {
            return loaderKind.Value.ToString();
        }

        return type switch
        {
            "release" => isChinese ? "原版发布" : "Vanilla release",
            "snapshot" => isChinese ? "原版快照" : "Vanilla snapshot",
            _ => isChinese ? "本地探测" : "Detected local",
        };
    }

    private static string SanitizeInstanceId(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return sanitized.Replace(' ', '-');
    }

    private static string BuildDetectedInstanceId(string installRoot, string launchVersionId)
    {
        var normalizedRoot = Path.GetFullPath(installRoot);
        var hashBytes = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedRoot));
        var hash = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return $"detected-{hash}-{SanitizeInstanceId(launchVersionId)}";
    }

    private IEnumerable<string> GetLaunchResourceRoots(Instance instance)
    {
        var roots = new List<string>();

        if (instance.GetIsDetected() && !string.IsNullOrWhiteSpace(instance.GetDetectedRoot()))
        {
            var detectedRoot = instance.GetDetectedRoot();
            var launchVersionId = instance.GetLaunchVersionId();
            roots.Add(detectedRoot);
            roots.Add(Path.Combine(detectedRoot, "versions", launchVersionId));
            roots.Add(Path.Combine(detectedRoot, "versions", launchVersionId, ".minecraft"));
        }

        roots.Add(Path.Combine(InstallPath, "instances", instance.GetId()));

        return roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetLaunchResourceDirectories()
    {
        return
        [
            "mods",
            "resourcepacks",
            "shaderpacks",
            "config",
            "defaultconfigs",
        ];
    }

    private static IReadOnlyList<string> GetLaunchRootFiles()
    {
        return
        [
            "options.txt",
            "optionsof.txt",
            "servers.dat",
        ];
    }

    private static string BuildIsolationResourceId(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        var hash = SHA1.HashData(stream);
        var extension = Path.GetExtension(sourcePath);
        return string.IsNullOrWhiteSpace(extension)
            ? Convert.ToHexString(hash).ToLowerInvariant()
            : $"{Convert.ToHexString(hash).ToLowerInvariant()}{extension.ToLowerInvariant()}";
    }

    private static string GetLauncherVersionLabel()
    {
        var informationalVersion = FileVersionInfo.GetVersionInfo(typeof(LauncherStateService).Assembly.Location).ProductVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion!;
        }

        var assemblyVersion = typeof(LauncherStateService).Assembly.GetName().Version;
        return assemblyVersion is null ? "0.1.0" : assemblyVersion.ToString(3);
    }

    private static string BuildInstanceVersionSummary(Instance instance, bool isChinese)
    {
        var launchVersion = instance.GetLaunchVersionId();
        var gameVersion = instance.GetGameVersionId();
        var loaderKind = instance.GetLoaderKind();
        var loaderVersion = instance.GetLoaderVersion();

        if (loaderKind is null)
        {
            return isChinese
                ? $"原版 {gameVersion}"
                : $"Vanilla {gameVersion}";
        }

        var loaderName = loaderKind.Value.ToString();
        return isChinese
            ? $"{loaderName} {loaderVersion} / MC {gameVersion}"
            : $"{loaderName} {loaderVersion} / MC {gameVersion}";
    }

    private static string BuildRuntimeSummary(
        string instanceName,
        string gameVersionId,
        string launchVersionId,
        LoaderKind? loaderKind,
        string? loaderVersion,
        bool isChinese)
    {
        if (loaderKind is null)
        {
            return isChinese
                ? $"{instanceName} 当前使用原版 {gameVersionId}。"
                : $"{instanceName} currently uses vanilla {gameVersionId}.";
        }

        var loaderName = loaderKind.Value.ToString();
        var versionPart = string.IsNullOrWhiteSpace(loaderVersion) ? loaderName : $"{loaderName} {loaderVersion}";
        return isChinese
            ? $"{instanceName} 当前绑定 {versionPart}，启动配置为 {launchVersionId}。"
            : $"{instanceName} is bound to {versionPart} with launch profile {launchVersionId}.";
    }
}
